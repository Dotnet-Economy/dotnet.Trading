# dotnet.Trading

Dotnet Economy Trading microservice

## Build the docker image

```powershell
$env:GH_OWNER="Dotnet-Economy"
$env:GH_PAT="[PAT HERE]"
$version="1.0.2"
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

## Creating the pod managed identity

```powershell
$namespace="trading"

az identity create -g $appname -n $namespace
$IDENTITY_RESOURCE_ID=az identity show -g $appname -n $namespace --query id -otsv

az aks pod-identity add -g $appname --cluster-name $appname --namespace $namespace -n $namespace --identity-resource-id $IDENTITY_RESOURCE_ID
```

## Granting acess to Key Vault secrets

```powershell
$IDENTITY_CLIENT_ID=az identity show -g $appname -n $namespace --query clientId -otsv
az keyvault set-policy -n $appname --secret-permissions get list --spn $IDENTITY_CLIENT_ID
```

## Creating the Kubernetes resources

```powershell
kubectl apply -f ./kubernetes/trading.yaml -n $namespace
```
