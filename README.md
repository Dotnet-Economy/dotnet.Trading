[![Trading CICD](https://github.com/Dotnet-Economy/dotnet.Trading/actions/workflows/cicd.yml/badge.svg?branch=main)](https://github.com/Dotnet-Economy/dotnet.Trading/actions/workflows/cicd.yml)

# dotnet.Trading

Dotnet Economy Trading microservice

## Build the docker image

```powershell
$env:GH_OWNER="Dotnet-Economy"
$env:GH_PAT="[PAT HERE]"
$version="1.0.3"
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

## Installing the Helm chart

```powershell
$helmUser=[guid]::Empty.Guid
$helmPassword=az acr login --name $appname --expose-token --output tsv --query accessToken

$env:HELM_EXPERIMENTAL_OCI=1

helm registry login "$appname.azurecr.io" --username  $helmUser --password $helmPassword

$chartVersion="0.1.0"
helm upgrade "$namespace-service" oci://$appname.azurecr.io/helm/microservice --version $chartVersion -f ./helm/values.yaml -n $namespace --install #--debug
```

## Required organisation secrets for Github workflow

GH_PAT: Created in GitHub user profile --> Settings --> Developer settings --> Personal access token
AZURE_TENANT_ID: From AAD properties page
AZURE_SUBSCRIPTION_ID: From Azure Portal Subscription
AZURE_CLIENT_ID: From AAD App registration
