namespace GridOps.Api.Common.Errors;

// -> 401. don't know who you are (bad login, no/invalid token)
public class UnauthorizedException(string message) : Exception(message);

// -> 403. know who you are, not allowed to do this
public class ForbiddenException(string message) : Exception(message);
