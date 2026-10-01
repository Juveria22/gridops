namespace GridOps.Api.Common.Errors;

// -> 404
public class NotFoundException(string resource, object key)
    : Exception($"{resource} {key} not found");
