using System;
using System.Data;

namespace QuanLyHangHoa.Data;

public sealed record DatabaseWriteRequest(
    string OperationName,
    Guid OperationId,
    IsolationLevel IsolationLevel = IsolationLevel.ReadCommitted);