from uuid import uuid4
from azureml.core import Workspace, Experiment
from azureml.core.authentication import ServicePrincipalAuthentication
from luna import utils
from Agent import key_vault_client
import json

class AzureMLUtils(object):
    """The utlitiy class to execute and monitor runs in AML"""
    
    def get_workspace_info_from_resource_id(self, resource_id):
        infoList = resource_id.split('/')
        subscriptionId = infoList[2]
        resourceGroupName = infoList[4]
        workspaceName = infoList[-1]
        return subscriptionId, resourceGroupName, workspaceName

    def __init__(self, workspace):
        secret = key_vault_client.get_secret(workspace.AADApplicationSecretName)
        auth = ServicePrincipalAuthentication(
            tenant_id = workspace.AADTenantId,
            service_principal_id = workspace.AADApplicationId,
            service_principal_password = secret.value)
        subscriptionId, resourceGroupName, workspaceName = self.get_workspace_info_from_resource_id(workspace.ResourceId)
        ws = Workspace(subscriptionId, resourceGroupName, workspaceName, auth)
        self._workspace = ws
        
    def runProject(self, productName, deploymentName, apiVersion, entryPoint, userInput, predecessorOperationid, userId, subscriptionId):
        operationId = str('a' + uuid4().hex[1:])
        experimentName = 'myexperiment'
        run_id = utils.RunProject(azureml_workspace = self._workspace, 
                                entry_point = entryPoint, 
                                experiment_name = experimentName, 
                                parameters={'modelId': operationId, 
                                            'userInput': userInput, 
                                            'operationId': operationId,
                                            'productName': productName,
                                            'deploymentName': deploymentName,
                                            'apiVersion': apiVersion,
                                            'subscriptionId': subscriptionId}, 
                                tags={'userId': userId, 
                                        'productName': productName, 
                                        'deploymentName': deploymentName, 
                                        'apiVersion': apiVersion,
                                        'operationName': entryPoint,
                                        'operationId': operationId,
                                        'subscriptionId': subscriptionId})
        return operationId

    def getOperationStatus(self, operationName, operationId, userId, subscriptionId):
        experimentName = 'myexperiment'
        exp = Experiment(self._workspace, experimentName)
        tags = {'userId': userId,
                'operationId': operationId,
                'operationName': operationName,
                'subscriptionId': subscriptionId}
        runs = exp.get_runs(type='azureml.PipelineRun', tags=tags)
        run = next(runs)
        result = {'operationId': operationId,
                  'status': run.status
            }
        return result

    def listAllOperations(self, operationName, userId, subscriptionId):
        experimentName = 'myexperiment'
        exp = Experiment(self._workspace, experimentName)
        tags = {'userId': userId,
                'operationName': operationName,
                'subscriptionId': subscriptionId}
        runs = exp.get_runs(type='azureml.PipelineRun', tags=tags)
        run = next(runs)
        resultList = []
        while True:
            result = {'operationId': run.tags["operationId"],
                    'status': run.status
                }
            resultList.append(result)
            try:
                run = next(runs)
            except StopIteration:
                break
        return resultList

    def getOperationOutput(self, operationName, operationId, userId, subscriptionId):
        experimentName = 'myexperiment'
        exp = Experiment(self._workspace, experimentName)
        tags = {'userId': userId,
                'operationId': operationId,
                'operationName': operationName,
                'subscriptionId': subscriptionId}
        runs = exp.get_runs(type='azureml.PipelineRun', tags=tags)
        run = next(runs)
        child_runs = run.get_children()
        child_run = next(child_runs)

        files = child_run.get_file_names()
        return {"operationId": operationId, "files": files}

    def listAllOperationOutputs(self, operationName, userId, subscriptionId):
        experimentName = 'myexperiment'
        exp = Experiment(self._workspace, experimentName)
        tags = {'userId': userId,
                'operationName': operationName,
                'subscriptionId': subscriptionId}
        runs = exp.get_runs(type='azureml.PipelineRun', tags=tags)
        run = next(runs)
        results = []
        while True:
            child_runs = run.get_children()
            child_run = next(child_runs)

            files = child_run.get_file_names()
            results.append({"operationId": run.tags["operationId"], "files": files})
            try:
                run = next(runs)
            except StopIteration:
                break
        return results

    def deleteOperationOutput(self, productName, deploymentName, apiVersion, operationName, operationId, userId, subscriptionId):
        return