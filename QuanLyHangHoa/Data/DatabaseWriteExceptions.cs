using System;

namespace QuanLyHangHoa.Data;

public abstract class DatabaseWriteException : Exception
{
    protected DatabaseWriteException(
        string code,
        Guid operationId,
        string message,
        Exception innerException)
        : base(message, innerException)
    {
        Code = code;
        OperationId = operationId;
    }

    public string Code { get; }

    public Guid OperationId { get; }
}

public sealed class DatabaseWriteConflictException : DatabaseWriteException
{
    internal DatabaseWriteConflictException(Guid operationId, Exception innerException)
        : base(
            "DB-WRITE-CONFLICT",
            operationId,
            "The data was changed by another client.",
            innerException)
    {
    }
}

public sealed class DatabaseWriteRetryExhaustedException : DatabaseWriteException
{
    internal DatabaseWriteRetryExhaustedException(Guid operationId, Exception innerException)
        : base(
            "DB-WRITE-RETRY-EXHAUSTED",
            operationId,
            "The database write could not complete after all retry attempts.",
            innerException)
    {
    }
}
