# dotnet.Trading

Dotnet Economy Trading microservice

## Build the docker image

```powershell
$env:GH_OWNER="Dotnet-Economy"
$env:GH_PAT="[PAT HERE]"
$version="1.0.1"
$appname="dotneteconomy"
docker build --secret id=GH_OWNER --secret id=GH_PAT -t "$appname.azurecr.io/dotnet.trading:$version" .
```

## Run the docker image

```powershell
$cosmosDbConnString="[CONN STRING HERE]"
$serviceBusConnString="[CONN STRING HERE]"
docker run -it --rm -p 5006:5006 --name trading -e MongoDbSettings__ConnectionString=$cosmosDbConnString -e ServiceBusSettings__ConnectionString=$serviceBusConnString -e ServiceSettings__MessageBroker="SERVICEBUS" dotnet.trading:$version
```

## Publishing the docker image

```powershell
az acr login --name $appname
docker push "$appname.azurecr.io/dotnet.trading:$version"
```
