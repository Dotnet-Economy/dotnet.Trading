# dotnet.Trading

Dotnet Economy Trading microservice

## Build the docker image

```powershell
$env:GH_OWNER="Dotnet-Economy"
$env:GH_PAT="[PAT HERE]"
$version="1.0.0"
docker build --secret id=GH_OWNER --secret id=GH_PAT -t dotnet.trading:$version .
```

## Run the docker image

```powershell
docker run -it --rm -p 5006:5006 --name trading -e MongoDbSettings__Host=mongo -e RabbitMQSettings__Host=rabbitmq --network dotnetinfra_default dotnet.trading:$version
```
