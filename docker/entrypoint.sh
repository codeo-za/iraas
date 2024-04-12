#!/bin/bash

set -e

envsubst '$FLUENTD_TAG $FLUENTD_HOST $FLUENTD_LOG_LEVEL' < "/app/log4net.config" > "/tmp/log4net.config"
mv /tmp/log4net.config /app/log4net.config

exec "$@"
