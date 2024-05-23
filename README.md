# Azure Red Hat OpenShift (ARO) Logs and Prometheus Metrics Forwarding to Azure Deployment Guide

This guide provides a comprehensive walkthrough for setting up Azure Red Hat OpenShift (ARO) with a focus on forwarding application logs to Azure Log Analytics and Prometheus metrics to Azure Monitor. It uses a more native approach suggested by Red Hat.

## Table of Contents

1. [Prerequisites](#prerequisites)
    1. [Creating an ARO Cluster and Logging in](#creating-an-aro-cluster-and-logging-in)
    2. [Deploying a .Net Application to ARO](#deploying-a-net-application-to-aro)
2. [Application Logs Forwarding to Azure Log Analytics](#application-logs-forwarding-to-azure-log-analytics)
    1. [Adding Pull Secret for OperatorHub](#adding-pull-secret-for-operatorhub)
    2. [Installing RedHat OpenShift Logging Operator](#installing-redhat-openshift-logging-operator)
    3. [Deploying Azure Log Analytics and Using Cluster Logging Forwarder](#deploying-azure-log-analytics-and-using-cluster-logging-forwarder)
2. [Prometheus Metrics Forwarding to Azure Monitor](#prometheus-metrics-forwarding-to-azure-monitor)

---

## Prerequisites

### Creating an ARO Cluster and Logging in

1. Create the ARO cluster:

    ```bash
    az aro create --resource-group ARO-LogAnalyticsPOC --name myAROCluster
    ```

2. Get ARO credentials:

    ```bash
    az aro list-credentials --name 'myAROCluster' --resource-group 'ARO-LogAnalyticsPOC'
    ```

### Deploying a .Net Application to ARO

1. Log in to ARO:

    ```bash
    oc login
    ```

2. Create a new project:

    ```bash
    oc new-project demoproject
    ```

3. Deploy the .Net application (make sure it's an .NET 8 application):

    ```bash
    oc new-app registry.access.redhat.com/ubi8/dotnet-80~<project-github-url>
    ```

4. Check the status:

    ```bash
    oc status
    ```

5. Expose the service:

    ```bash
    oc expose service/demoapp
    ```

6. Get the route:

    ```bash
    oc get route demoapp
    ```

## Application Logs Forwarding to Azure Log Analytics

### Adding Pull Secret for OperatorHub
**(Please skip this step if you already have a pull secret configured in your ARO cluster)**

To add a pull secret in Azure Red Hat OpenShift for accessing OperatorHub, please follow the detailed steps provided in the official Microsoft documentation. 

[Follow these steps to add or update a pull secret.](https://learn.microsoft.com/en-us/azure/openshift/howto-add-update-pull-secret)

### Installing Red Hat OpenShift Logging Operator

To install Red Hat OpenShift Logging Operator from OperatorHub, please follow the detailed steps provided in the official Red Hat OpenShift documentation. 

[Follow these steps to install Red Hat OpenShift Logging Operator.](https://docs.openshift.com/container-platform/4.7/logging/cluster-logging-deploying.html#:~:text=Install%20the%20Red%20Hat%20OpenShift%20Logging%20Operator%3A%20In,the%20list%20of%20available%20Operators%2C%20and%20click%20Install.)

### Deploying Azure Log Analytics and Using Cluster Logging Forwarder

1. Open WSL and log in to Azure:

    ```bash
    az login
    ```

2. Set environment variables:

    ```bash
    export AZR_RESOURCE_LOCATION=eastus
    export AZR_RESOURCE_GROUP=ARO-LogAnalyticsPOC
    export AZR_LOG_APP_NAME=aro-loganalytics
    ```

3. Add the Azure CLI log extensions:

    ```bash
    az extension add --name log-analytics
    ```

4. Create a Log Analytics workspace:

    ```bash
    az monitor log-analytics workspace create -g $AZR_RESOURCE_GROUP -n $AZR_LOG_APP_NAME -l $AZR_RESOURCE_LOCATION
    ```

5. Retrieve the workspace ID and shared key:

    ```bash
    WORKSPACE_ID=$(az monitor log-analytics workspace show -g $AZR_RESOURCE_GROUP -n $AZR_LOG_APP_NAME --query customerId -o tsv)
    SHARED_KEY=$(az monitor log-analytics workspace get-shared-keys -g $AZR_RESOURCE_GROUP -n $AZR_LOG_APP_NAME --query primarySharedKey -o tsv)
    ```

6. Create a secret to hold the shared key:

    ```bash
    oc -n openshift-logging create secret generic azure-monitor-shared-key --from-literal=shared_key='YOUR_SHARED_KEY'
    ```

7. Create a `ClusterLogging` resource and name it `clusterlogging.yaml`:

    ```yaml
    apiVersion: logging.openshift.io/v1
    kind: ClusterLogging
    metadata:
      name: instance
      namespace: openshift-logging
    spec:
      collection:
        type: vector
        vector: {}
    ```
    Deploy resource 

    ```bash
    oc apply -f clusterlogging.yaml
    ```


8. Create a `ClusterLogForwarder` resource and name it `clusterlogforwarder.yaml`:

    ```yaml
    apiVersion: logging.openshift.io/v1
    kind: ClusterLogForwarder
    metadata:
      name: instance
      namespace: openshift-logging
    spec:
      inputs:
      - name: my-app-logs
        application:
          namespaces:
          - demoproject
      outputs:
      - name: azure-monitor-app
        type: azureMonitor
        azureMonitor:
          customerId: $WORKSPACE_ID
          logType: aro_application_logs
        secret:
          name: azure-monitor-shared-key
      pipelines:
      - name: app-pipeline
        inputRefs:
        - my-app-logs
        outputRefs:
        - azure-monitor-app
    ```
    Deploy resource 

    ```bash
    oc apply -f clusterlogforwarder.yaml
    ```

    Change `application` to `infrastructure` if we want to forward infrastructure logs. 

9. Check the pod status:

    ```bash
    oc get pods -n openshift-logging
    ```

10. Check logs in Azure Log Analytics:
    1. Go to Log Analytics Workspaces -> Logs -> Select scope -> apply.
    2. Use the query:

        ```kusto
        aro_application_logs_CL
        | take 10
        ```

---

## Prometheus Metrics Forwarding to Azure Monitor

For officals documentation, please follow the link below. 
[Send data to an Azure Monitor workspace from your Prometheus server](https://learn.microsoft.com/en-us/azure/openshift/howto-remotewrite-prometheus)

### Registering an Application and Creating a Service Principal

1. [Register an application with Microsoft Entra ID and create a service principal.](https://learn.microsoft.com/en-us/entra/identity-platform/howto-create-service-principal-portal#register-an-application-with-azure-ad-and-create-a-service-principal)
2. Copy the Client ID and Tenant ID.
3. [Create a new client secret and copy the value.](https://learn.microsoft.com/en-us/entra/identity-platform/howto-create-service-principal-portal#option-3-create-a-new-client-secret)

### Setting Environment Variables

1. Set environment variables:

    ```bash
    export TENANT_ID='YOUR_TENANT_ID'
    export CLIENT_ID='YOUR_CLIENT_ID'
    export CLIENT_SECRET='YOUR_CLIENT_SECRET'
    ```

### Creating an Azure Monitor Workspace

1. [Create an Azure Monitor Workspace in Azure portal](https://learn.microsoft.com/en-us/azure/azure-monitor/essentials/azure-monitor-workspace-manage?tabs=azure-portal#create-an-azure-monitor-workspace).

### Assigning Monitoring Metrics Publisher Role

The application must have the Monitoring Metrics Publisher role for the data collection rule that is associated with your Azure Monitor workspace.

1. In the Azure portal, go to the instance of Azure Monitor for your subscription.

2. On the resource menu, select **Data Collection Rules**.

3. Select the data collection rule that is associated with your Azure Monitor workspace.

4. On the **Overview** page for the data collection rule, select **Access control (IAM)**.

5. Select **Add**, and then select **Add role assignment**.

6. Select the **Monitoring Metrics Publisher** role, and then select **Next**.

7. Select **User, group, or service principal**, and then choose **Select members**. Select the application that you registered, and then choose **Select**.

8. To complete the role assignment, select **Review + assign**.

### Creating a Secret in OpenShift

1. Create a secret in OpenShift and name it `secret.yaml`:

    ```yaml
    apiVersion: v1
    kind: Secret
    metadata:
      name: oauth2-credentials
      namespace: openshift-monitoring
    stringData:
      id: "${CLIENT_ID}"
      secret: "${CLIENT_SECRET}"
    ```
    Deploy secret 

    ```bash
    oc apply -f secret.yaml
    ```
### Editing Cluster Monitoring Configuration

1. Open the config map file for editing

```bash
oc edit -n openshift-monitoring cm cluster-monitoring-config
```

2. Edit the `cluster-monitoring-config.yaml` file and apply changes:

    ```yaml
    data:
      config.yaml: |
        prometheusK8s:
          remoteWrite:
            - url: "<INGESTION-URL>"
              oauth2:
                clientId:
                  secret:
                    name: oauth2-credentials
                    key: id
                clientSecret:
                  name: oauth2-credentials
                  key: secret
                tokenUrl: "https://login.microsoftonline.com/${TENANT_ID}/oauth2/v2.0/token"
                scopes:
                  - "https://monitor.azure.com/.default"
    ```

    Update the config map file:

    1. Replace `INGESTION-URL` in the config map file with the value for **Metrics ingestion endpoint** from the **Overview** page for the Azure Monitor workspace.

    2. Replace `TENANT_ID` in the config map file with the tenant ID of the service principal.
    Deploy Cluster Monitoring Configuration

2. Apply the configuration:

    ```bash
    oc apply -f cluster-monitoring-config.yaml
    ```

### Restarting Prometheus Pods

1. Get Prometheus pods names:

    ```bash
    oc get pods -n openshift-monitoring
    ```

2. Restart Prometheus pods:

    ```bash
    oc delete pod <prometheus-pods-name> -n openshift-monitoring
    oc delete pod <prometheus-pods-name> -n openshift-monitoring
    ```

3. Verify new Prometheus pods are running:

    ```bash
    oc get pods -n openshift-monitoring
    ```

### Using PromQL to Calculate CPU Usage

Navigate to your Azure Monitor Workspace -> Prometheus explorer

1. Use PromQL to calculate the percentage of CPU usage:

    ```prometheus
    100 * (1 - avg by(instance) (rate(node_cpu_seconds_total{mode="idle"}[5m])))
    ```

---

**Note:** Replace placeholders like `YOUR_SHARED_KEY`, `YOUR_TENANT_ID`, `YOUR_CLIENT_ID`, and `YOUR_CLIENT_SECRET` with actual values. This guide ensures that sensitive information such as actual credentials is excluded for security purposes.