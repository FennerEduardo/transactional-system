import { test } from 'node:test';
import * as cdk from 'aws-cdk-lib';
import { Match, Template } from 'aws-cdk-lib/assertions';
import { TransactionalSystemInfrastructureStack } from '../lib/transactional-system-stack';

const template = Template.fromStack(new TransactionalSystemInfrastructureStack(new cdk.App(), 'Test'));

test('domain events fan out from an SNS FIFO topic to an SQS FIFO queue', () => {
  template.hasResourceProperties('AWS::SNS::Topic', { FifoTopic: true, ContentBasedDeduplication: true });
  template.hasResourceProperties('AWS::SNS::Subscription', { Protocol: 'sqs', RawMessageDelivery: true });
});

test('failed messages go to a dead-letter queue after three attempts and raise an alarm', () => {
  template.hasResourceProperties('AWS::SQS::Queue', { RedrivePolicy: Match.objectLike({ maxReceiveCount: 3 }) });
  template.resourceCountIs('AWS::CloudWatch::Alarm', 1);
});

test('read models are retained and recoverable', () => {
  template.hasResource('AWS::DynamoDB::Table', {
    DeletionPolicy: 'Retain',
    Properties: Match.objectLike({ BillingMode: 'PAY_PER_REQUEST', PointInTimeRecoverySpecification: { PointInTimeRecoveryEnabled: true } })
  });
});

test('the service account role is assumed through the cluster OIDC provider (IRSA)', () => {
  template.hasResourceProperties('AWS::IAM::Role', {
    AssumeRolePolicyDocument: Match.objectLike({
      Statement: Match.arrayWith([Match.objectLike({ Action: 'sts:AssumeRoleWithWebIdentity' })])
    })
  });
});
