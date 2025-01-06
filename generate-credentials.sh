#!/bin/bash

set -e

AWS_KEY_EXPIRY_MINUTES=30

generate_temporary_aws_credentials() {
  entries=()
  unset AWS_SESSION_TOKEN
  aws_credentials=$(aws sts get-session-token --duration-seconds $((AWS_KEY_EXPIRY_MINUTES * 60)) --region $AWS_REGION)
  aws_temp_access_key=$(echo $aws_credentials | jq -r '.Credentials.AccessKeyId')
  aws_temp_access_secret=$(echo $aws_credentials | jq -r '.Credentials.SecretAccessKey')
  aws_temp_aws_session_token=$(echo $aws_credentials | jq -r '.Credentials.SessionToken')
  entries+=( "AWS_SESSION_TOKEN=$aws_temp_aws_session_token" )
  for entry in "${entries[@]}"; do
    echo "$entry" >> $GITHUB_ENV
  done
}
mask_secrets_in_github_actions() {
  if [ "$GITHUB_ACTIONS" ]; then
    echo "masking secrets in GitHub Actions"
    for entry in "${entries[@]}"; do
      key=$(echo "$entry" | cut -d '=' -f1)
      value=$(echo "$entry" | cut -d '=' -f2-)
      if echo "$key" | grep -qiE '(token|key|pass|secret)'; then
        echo "will mask $key for remainder of script"
        echo "::add-mask::$value"
      fi
    done
  fi
}
generate_temporary_aws_credentials
mask_secrets_in_github_actions
