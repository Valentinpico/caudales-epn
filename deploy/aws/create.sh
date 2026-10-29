#!/usr/bin/env bash
# One-time creation of the on-demand Caudales box (AWS account 087770005457, us-east-1).
# Creates: security group, key pair, Elastic IP, EC2 t3.small (Ubuntu 24.04, Docker, 2 GB swap),
# CloudWatch alarm that stops the box after 10 min idle.
# Idempotent: re-running reuses what exists. State: ~/.caudales-aws/state.env
set -euo pipefail

export AWS_REGION=us-east-1
ACCOUNT=087770005457
NAME=caudales-demo
TYPE=t3.small
DISK_GB=20
STATE_DIR="$HOME/.caudales-aws"
STATE="$STATE_DIR/state.env"
KEY="$STATE_DIR/$NAME.pem"
HERE="$(cd "$(dirname "$0")" && pwd)"

mkdir -p "$STATE_DIR" && chmod 700 "$STATE_DIR"
[ "$(aws sts get-caller-identity --query Account --output text)" = "$ACCOUNT" ] \
  || { echo "wrong AWS account (want $ACCOUNT)"; exit 1; }

save() { grep -v "^$1=" "$STATE" 2>/dev/null > "$STATE.tmp" || true; echo "$1=$2" >> "$STATE.tmp"; mv "$STATE.tmp" "$STATE"; }
[ -f "$STATE" ] && source "$STATE"

if [ ! -f "$KEY" ]; then
  aws ec2 delete-key-pair --key-name $NAME >/dev/null 2>&1 || true
  aws ec2 create-key-pair --key-name $NAME --query KeyMaterial --output text > "$KEY"
  chmod 600 "$KEY"; echo "key pair created"
fi

# 80/443 public; 22 is opened per run for this machine only (see the control script).
VPC=$(aws ec2 describe-vpcs --filters Name=isDefault,Values=true --query 'Vpcs[0].VpcId' --output text)
SG=$(aws ec2 describe-security-groups --filters Name=group-name,Values=$NAME Name=vpc-id,Values=$VPC \
  --query 'SecurityGroups[0].GroupId' --output text)
if [ "$SG" = "None" ]; then
  SG=$(aws ec2 create-security-group --group-name $NAME --description "caudales on-demand demo" --vpc-id $VPC --query GroupId --output text)
  for p in 80 443; do aws ec2 authorize-security-group-ingress --group-id $SG --protocol tcp --port $p --cidr 0.0.0.0/0 >/dev/null; done
  echo "security group $SG created"
fi
save SG "$SG"

# Elastic IP keeps the URL stable across stop/start.
if [ -z "${ALLOC:-}" ]; then
  ALLOC=$(aws ec2 allocate-address --domain vpc --tag-specifications "ResourceType=elastic-ip,Tags=[{Key=Name,Value=$NAME}]" --query AllocationId --output text)
  echo "elastic ip allocated"
fi
IP=$(aws ec2 describe-addresses --allocation-ids "$ALLOC" --query 'Addresses[0].PublicIp' --output text)
save ALLOC "$ALLOC"; save IP "$IP"; save DOMAIN "${IP//./-}.sslip.io"

if [ -z "${INSTANCE:-}" ] || [ "$(aws ec2 describe-instances --instance-ids "$INSTANCE" --query 'Reservations[0].Instances[0].State.Name' --output text 2>/dev/null)" = "terminated" ]; then
  AMI=$(aws ssm get-parameter --name /aws/service/canonical/ubuntu/server/24.04/stable/current/amd64/hvm/ebs-gp3/ami-id --query Parameter.Value --output text)
  INSTANCE=$(aws ec2 run-instances --image-id "$AMI" --instance-type $TYPE --key-name $NAME \
    --security-group-ids "$SG" \
    --metadata-options HttpTokens=required \
    --block-device-mappings "DeviceName=/dev/sda1,Ebs={VolumeSize=$DISK_GB,VolumeType=gp3,DeleteOnTermination=true}" \
    --user-data "file://$HERE/user-data.sh" \
    --tag-specifications "ResourceType=instance,Tags=[{Key=Name,Value=$NAME}]" \
    --query 'Instances[0].InstanceId' --output text)
  echo "instance $INSTANCE launched"
  aws ec2 wait instance-running --instance-ids "$INSTANCE"
  aws ec2 associate-address --instance-id "$INSTANCE" --allocation-id "$ALLOC" >/dev/null
fi
save INSTANCE "$INSTANCE"

# Auto-stop: average CPU < 5 % for 2 x 5 min -> stop. The waker Lambda resets it to OK on every
# start, because alarm actions only fire on a state transition.
aws cloudwatch put-metric-alarm --alarm-name "$NAME-idle-stop" \
  --namespace AWS/EC2 --metric-name CPUUtilization --dimensions Name=InstanceId,Value="$INSTANCE" \
  --statistic Average --period 300 --evaluation-periods 2 --threshold 5 \
  --comparison-operator LessThanThreshold --treat-missing-data notBreaching \
  --alarm-actions "arn:aws:automate:$AWS_REGION:ec2:stop"

echo
echo "ready: instance $INSTANCE  ip $IP  url https://${IP//./-}.sslip.io"
echo "next: deploy/aws/caudales deploy"
