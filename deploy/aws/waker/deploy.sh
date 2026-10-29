#!/usr/bin/env bash
# Create or update the waker Lambda: Function URL (public), IAM role limited to the listed boxes,
# and an EventBridge rule that resets each box's idle alarm when it starts. Idempotent.
# Apps: Caudales (~/.caudales-aws/state.env) and the RAG test box (~/.ragnew-aws/state.env).
set -euo pipefail

export AWS_REGION=us-east-1
ACCOUNT=087770005457
NAME=demo-waker
HERE="$(cd "$(dirname "$0")" && pwd)"
[ "$(aws sts get-caller-identity --query Account --output text)" = "$ACCOUNT" ] \
  || { echo "wrong AWS account (want $ACCOUNT)"; exit 1; }

CAUDALES_INSTANCE=$(. ~/.caudales-aws/state.env; echo "$INSTANCE")
CAUDALES_URL=$(. ~/.caudales-aws/state.env; echo "https://$DOMAIN")
RAG_INSTANCE=$(. ~/.ragnew-aws/state.env; echo "$INSTANCE")
RAG_URL=$(. ~/.ragnew-aws/state.env; echo "https://$DOMAIN")

APPS=$(cat <<EOF
{"caudales":{"name":"Caudales — series hidrometeorológicas","instance":"$CAUDALES_INSTANCE","url":"$CAUDALES_URL","alarm":"caudales-demo-idle-stop"},
 "rag":{"name":"Asistente RAG","instance":"$RAG_INSTANCE","url":"$RAG_URL","alarm":"ragnew-test-idle-stop"}}
EOF
)
ARNS="\"arn:aws:ec2:$AWS_REGION:$ACCOUNT:instance/$CAUDALES_INSTANCE\",\"arn:aws:ec2:$AWS_REGION:$ACCOUNT:instance/$RAG_INSTANCE\""
ALARMS="\"arn:aws:cloudwatch:$AWS_REGION:$ACCOUNT:alarm:caudales-demo-idle-stop\",\"arn:aws:cloudwatch:$AWS_REGION:$ACCOUNT:alarm:ragnew-test-idle-stop\""

# --- IAM role ----------------------------------------------------------------------
if ! aws iam get-role --role-name $NAME >/dev/null 2>&1; then
  aws iam create-role --role-name $NAME --assume-role-policy-document '{
    "Version":"2012-10-17","Statement":[{"Effect":"Allow","Principal":{"Service":"lambda.amazonaws.com"},"Action":"sts:AssumeRole"}]}' >/dev/null
  aws iam attach-role-policy --role-name $NAME --policy-arn arn:aws:iam::aws:policy/service-role/AWSLambdaBasicExecutionRole
  echo "role created"; sleep 10
fi
aws iam put-role-policy --role-name $NAME --policy-name wake --policy-document "{
  \"Version\":\"2012-10-17\",\"Statement\":[
    {\"Effect\":\"Allow\",\"Action\":\"ec2:DescribeInstances\",\"Resource\":\"*\"},
    {\"Effect\":\"Allow\",\"Action\":\"ec2:StartInstances\",\"Resource\":[$ARNS]},
    {\"Effect\":\"Allow\",\"Action\":\"cloudwatch:SetAlarmState\",\"Resource\":[$ALARMS]}]}"

# --- Function ----------------------------------------------------------------------
ZIP=$(mktemp -d)/waker.zip
(cd "$HERE" && zip -q "$ZIP" handler.py)
ENV=$(python3 -c 'import json,sys; print(json.dumps({"Variables": {"APPS": sys.argv[1]}}))' "$APPS")
if aws lambda get-function --function-name $NAME >/dev/null 2>&1; then
  aws lambda update-function-code --function-name $NAME --zip-file "fileb://$ZIP" >/dev/null
  aws lambda wait function-updated --function-name $NAME
  aws lambda update-function-configuration --function-name $NAME --environment "$ENV" >/dev/null
else
  aws lambda create-function --function-name $NAME --runtime python3.12 --handler handler.handler \
    --role "arn:aws:iam::$ACCOUNT:role/$NAME" --zip-file "fileb://$ZIP" --timeout 10 --memory-size 128 \
    --environment "$ENV" >/dev/null
  aws lambda wait function-active --function-name $NAME
  aws lambda create-function-url-config --function-name $NAME --auth-type NONE >/dev/null
  aws lambda add-permission --function-name $NAME --statement-id url-public \
    --action lambda:InvokeFunctionUrl --principal '*' --function-url-auth-type NONE >/dev/null
  aws lambda add-permission --function-name $NAME --statement-id url-invoke \
    --action lambda:InvokeFunction --principal '*' --invoked-via-function-url >/dev/null
fi
aws lambda wait function-updated --function-name $NAME
rm -rf "$(dirname "$ZIP")"

# --- EventBridge: instance -> running resets its idle alarm --------------------------
RULE_ARN=$(aws events put-rule --name $NAME-started --event-pattern "{
  \"source\":[\"aws.ec2\"],\"detail-type\":[\"EC2 Instance State-change Notification\"],
  \"detail\":{\"state\":[\"running\"],\"instance-id\":[\"$CAUDALES_INSTANCE\",\"$RAG_INSTANCE\"]}}" --query RuleArn --output text)
aws lambda add-permission --function-name $NAME --statement-id events-started \
  --action lambda:InvokeFunction --principal events.amazonaws.com --source-arn "$RULE_ARN" >/dev/null 2>&1 || true
aws events put-targets --rule $NAME-started --targets "Id=waker,Arn=arn:aws:lambda:$AWS_REGION:$ACCOUNT:function:$NAME" >/dev/null

URL=$(aws lambda get-function-url-config --function-name $NAME --query FunctionUrl --output text)
echo "waker: ${URL}caudales   ${URL}rag"
