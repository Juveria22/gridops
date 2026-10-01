namespace GridOps.Api.Common.Errors;

// -> 409. request is valid but breaks a business rule for the current state
public class ConflictException(string message) : Exception(message);
