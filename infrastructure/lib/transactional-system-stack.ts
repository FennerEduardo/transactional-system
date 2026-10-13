import * as cdk from 'aws-cdk-lib';
import { Construct } from 'constructs';
import * as cloudwatch from 'aws-cdk-lib/aws-cloudwatch';
import * as dynamodb from 'aws-cdk-lib/aws-dynamodb';
import * as ec2 from 'aws-cdk-lib/aws-ec2';
import * as eks from 'aws-cdk-lib/aws-eks';
import * as secretsmanager from 'aws-cdk-lib/aws-secretsmanager';
import * as sns from 'aws-cdk-lib/aws-sns';
import * as subscriptions from 'aws-cdk-lib/aws-sns-subscriptions';
import * as sqs from 'aws-cdk-lib/aws-sqs';
import { KubectlV35Layer } from '@aws-cdk/lambda-layer-kubectl-v35';

export interface TransactionalSystemInfrastructureStackProps extends cdk.StackProps {
  /** Kubernetes namespace of the service account. */
  readonly namespace?: string;
}

export class TransactionalSystemInfrastructureStack extends cdk.Stack {
  readonly domainEventsTopic: sns.Topic;
  readonly consumerQueue: sqs.Queue;
  readonly deadLetterQueue: sqs.Queue;
  readonly readModelTable: dynamodb.Table;

  constructor(scope: Construct, id: string, props: TransactionalSystemInfrastructureStackProps = {}) {
    super(scope, id, props);

    // Domain events: SNS FIFO topic fanned out to an SQS FIFO consumer queue.
    this.domainEventsTopic = new sns.Topic(this, 'DomainEventsTopic', {
      fifo: true,
      contentBasedDeduplication: true,
      displayName: 'Domain events'
    });

    // Messages that fail three times land in the DLQ and raise an alarm.
    this.deadLetterQueue = new sqs.Queue(this, 'DeadLetterQueue', {
      fifo: true,
      retentionPeriod: cdk.Duration.days(14),
      enforceSSL: true
    });
    this.consumerQueue = new sqs.Queue(this, 'ConsumerQueue', {
      fifo: true,
      contentBasedDeduplication: true,
      visibilityTimeout: cdk.Duration.seconds(30),
      enforceSSL: true,
      deadLetterQueue: { maxReceiveCount: 3, queue: this.deadLetterQueue }
    });
    this.domainEventsTopic.addSubscription(new subscriptions.SqsSubscription(this.consumerQueue, { rawMessageDelivery: true }));

    new cloudwatch.Alarm(this, 'DeadLetterQueueAlarm', {
      alarmDescription: 'Messages are failing and accumulating in the dead-letter queue.',
      metric: this.deadLetterQueue.metricApproximateNumberOfMessagesVisible({ period: cdk.Duration.minutes(1) }),
      threshold: 1,
      evaluationPeriods: 1,
      comparisonOperator: cloudwatch.ComparisonOperator.GREATER_THAN_OR_EQUAL_TO_THRESHOLD,
      treatMissingData: cloudwatch.TreatMissingData.NOT_BREACHING
    });

    // Read models / projections.
    this.readModelTable = new dynamodb.Table(this, 'ReadModelTable', {
      partitionKey: { name: 'pk', type: dynamodb.AttributeType.STRING },
      sortKey: { name: 'sk', type: dynamodb.AttributeType.STRING },
      billingMode: dynamodb.BillingMode.PAY_PER_REQUEST,
      pointInTimeRecoverySpecification: { pointInTimeRecoveryEnabled: true },
      removalPolicy: cdk.RemovalPolicy.RETAIN
    });

    const databaseSecret = new secretsmanager.Secret(this, 'DatabaseCredentials', {
      description: 'Database credentials for the service',
      generateSecretString: {
        secretStringTemplate: JSON.stringify({ username: 'app' }),
        generateStringKey: 'password',
        excludePunctuation: true
      }
    });

    // EKS cluster; the service account gets AWS permissions through IRSA (no node-wide credentials).
    const vpc = new ec2.Vpc(this, 'Vpc', { maxAzs: 2, natGateways: 1 });
    const cluster = new eks.Cluster(this, 'Cluster', {
      vpc,
      version: eks.KubernetesVersion.V1_35,
      kubectlLayer: new KubectlV35Layer(this, 'KubectlLayer'),
      defaultCapacity: 2,
      defaultCapacityInstance: ec2.InstanceType.of(ec2.InstanceClass.T3, ec2.InstanceSize.MEDIUM)
    });
    const serviceAccount = cluster.addServiceAccount('ServiceAccount', {
      name: 'transactional-system',
      namespace: props.namespace ?? 'default'
    });

    // Least privilege: publish events, consume the queue, read/write read models, read the DB secret.
    this.domainEventsTopic.grantPublish(serviceAccount);
    this.consumerQueue.grantConsumeMessages(serviceAccount);
    this.readModelTable.grantReadWriteData(serviceAccount);
    databaseSecret.grantRead(serviceAccount);

    new cdk.CfnOutput(this, 'DomainEventsTopicArn', { value: this.domainEventsTopic.topicArn });
    new cdk.CfnOutput(this, 'ConsumerQueueUrl', { value: this.consumerQueue.queueUrl });
    new cdk.CfnOutput(this, 'ServiceAccountRoleArn', { value: serviceAccount.role.roleArn });
  }
}
