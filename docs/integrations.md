# Adding an integration

The TypeScript testing-core reads integration flags and non-secret settings through IConfigPort, then its orchestration factories compose a concrete adapter and use cases. The C# equivalent has the same boundaries: IConfigPort and ISecretPort are ports, EnvConfigAdapter reads environment variables, and IntegrationRegistry composes typed infrastructure factories.

1. Define the new capability as a port in TestingCore.Ports when application code needs it. Keep vendor types out of the port.
2. Register an IntegrationDefinition with a name, environment prefix, and required non-secret settings. For example, audit + AUDIT + endpoint maps AUDIT_ENABLED and AUDIT_ENDPOINT.
3. Implement IIntegrationFactory<TServices> in infrastructure. Its constructor can accept transport dependencies such as HttpClient. Its Create method receives the non-secret IntegrationOptions and ISecretPort. Pass the secret port to a credential provider so the secret is read only when a call is made.
4. Register the factory in the composition root, then call Create<TServices>("audit"). Disabled integrations return null. A missing registration or wrong service type throws before reading configuration or secrets.
5. Test the disabled path, enabled construction, late secret read, and adapter contract with fake ports.

Azure's factory is AzureDevOpsIntegrationFactory(HttpClient). It produces IntegrationServices with TestManagement, IssueTracking, and Traceability ports. AzureCredentialProvider reads SYSTEM_ACCESSTOKEN for pipeline runs or AZDO_PAT for local runs at request time. Do not place tokens in IntegrationOptions or test metadata.

The registry is generic so a new integration can return its own service interface without adding properties to IntegrationServices. The concrete factory lives in infrastructure; TestingCore.Domain remains independent of xUnit, HTTP, and Azure.