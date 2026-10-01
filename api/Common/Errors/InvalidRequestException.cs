namespace GridOps.Api.Common.Errors;

// -> 400 with same shape as attribute validation errors
// for checks that need the db, e.g. crew id doesn't exist
public class InvalidRequestException(string field, string error) : Exception(error)
{
    public string Field { get; } = field;
}
