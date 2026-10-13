import * as cdk from 'aws-cdk-lib';
import { TransactionalSystemInfrastructureStack } from '../lib/transactional-system-stack';

const app = new cdk.App();
// Environment-agnostic by default; set CDK_DEFAULT_ACCOUNT / CDK_DEFAULT_REGION (or a profile) to deploy.
new TransactionalSystemInfrastructureStack(app, 'TransactionalSystem', {
  env: process.env.CDK_DEFAULT_ACCOUNT ? { account: process.env.CDK_DEFAULT_ACCOUNT, region: process.env.CDK_DEFAULT_REGION } : undefined
});
