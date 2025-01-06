#!/bin/bash

set -e

VAULT="Infra - YUMBI"
AWS_KEY_EXPIRY_MINUTES=30

fetch_aws_secrets() {
  aws_access_key=$(op read "op://${VAULT}/Yumbi.Web/AWS_ACCESS_KEY_ID" | tr -d '\n')
  aws_access_secret=$(op read "op://${VAULT}/Yumbi.Web/AWS_SECRET_ACCESS_KEY" | tr -d '\n')
  aws_account_id=$(op read "op://${VAULT}/Yumbi.Web/AWS_ACCOUNT_ID" | tr -d '\n')
  aws_region=$(op read "op://${VAULT}/Yumbi.Web/AWS_REGION" | tr -d '\n')
  github_nuget_user=$(op read "op://${VAULT}/Yumbi.Web/GITHUB_NUGET_USER" | tr -d '\n')
  github_nuget_token=$(op read "op://${VAULT}/Yumbi.Web/GITHUB_NUGET_TOKEN" | tr -d '\n')
}
generate_temporary_aws_credentials() {
  entries=()
  aws_get_temp_creds=(
    "export AWS_ACCESS_KEY_ID=$aws_access_key"
    "export AWS_SECRET_ACCESS_KEY=$aws_access_secret"
    "unset AWS_SESSION_TOKEN"
  )
  echo "generating temporary aws credentials"
  for cmd in "${aws_get_temp_creds[@]}"; do
    eval "$cmd"
  done
  aws_credentials=$(aws sts get-session-token --duration-seconds $((AWS_KEY_EXPIRY_MINUTES * 60)) --region $aws_region)
  aws_temp_access_key=$(echo $aws_credentials | jq -r '.Credentials.AccessKeyId')
  aws_temp_access_secret=$(echo $aws_credentials | jq -r '.Credentials.SecretAccessKey')
  aws_temp_aws_session_token=$(echo $aws_credentials | jq -r '.Credentials.SessionToken')
  entries+=( "AWS_ACCESS_KEY_ID=$aws_temp_access_key" )
  entries+=( "AWS_SECRET_ACCESS_KEY=$aws_temp_access_secret" )
  entries+=( "AWS_SESSION_TOKEN=$aws_temp_aws_session_token" )
  entries+=( "AWS_ACCOUNT_ID=$aws_account_id" )
  entries+=( "AWS_REGION=$aws_region" )
  entries+=( "GITHUB_NUGET_USER=$github_nuget_user" )
  entries+=( "GITHUB_NUGET_TOKEN=$github_nuget_token" )
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
fetch_aws_secrets
generate_temporary_aws_credentials
mask_secrets_in_github_actions
