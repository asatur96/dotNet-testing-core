# HTTP artifacts and recorded evidence

The TypeScript HTTP port returns a full API artifact and attaches it to an active step. IHttpTestClient keeps that return shape: SendAsync returns the request and response bodies so API assertions can inspect them.

HttpTestClient records a separate copy in TestContext. By default, MinimalHttpArtifactEvidencePolicy removes request and response bodies, URL query parameters, fragments, and URL user information from the recorded copy. It preserves method, URL path, status, duration, and timestamp. An absent request body remains null; a nonempty body becomes [omitted]. The raw return value and actual HTTP request are unchanged. HTTP calls outside an active step return normally and have no step artifact.

    var client = new HttpTestClient(httpClient, context);
    await context.StepAsync("submit command", "200", async () =>
    {
        var response = await client.SendAsync(HttpMethod.Post, "/commands", command);
        new ApiAssertions(context).ShouldHaveStatus(response, 200);
    });

To use another evidence rule, implement IHttpArtifactEvidencePolicy in the consumer and pass it as the third HttpTestClient constructor argument. Prepare receives the raw ApiArtifact and returns the copy to record. For example, a backend may allow selected JSON fields in evidence while omitting credentials and account data. Treat any custom policy as part of the evidence security boundary; a policy that returns the raw artifact stores full bodies in run records. URL paths, step action and expected text are authored separately and should not contain secrets.

This policy applies to HttpTestClient only. Direct service tests can attach other artifact types, and their owners must decide what those artifacts contain. ApiAssertions.ShouldHaveJsonValue masks values for known sensitive property names and also accepts sensitive: true for other fields. The separate raw API artifact remains available to test code until it is discarded.