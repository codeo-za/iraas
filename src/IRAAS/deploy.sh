#!/bin/sh
SERVICE_NAME="#{IRAAS_Server_PM2_Name}"
pm2 delete $SERVICE_NAME -s
pm2 start dotnet --name $SERVICE_NAME --log-date-format="YYYY-MM-DD HH:mm:ss" -- IRAAS.dll
pm2 save