FROM mcr.microsoft.com/dotnet/aspnet:8.0-jammy-amd64

RUN apt-get update && \
    apt-get install -y gettext-base

WORKDIR /app

COPY ./src/IRAAS/bin/ReleaseForDocker/net8.0/publish .

COPY docker/entrypoint.sh entrypoint.sh
RUN chmod +x entrypoint.sh

ENTRYPOINT ["/app/entrypoint.sh"]

CMD ["./IRAAS"]
