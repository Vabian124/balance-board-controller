namespace BalanceBoard.Core.Services;

/// <summary>Emits structured exception lines through session log callbacks (no FileLogService dependency).</summary>
public static class SessionExceptionLog
{
    public static void Emit(Action<string>? log, Exception exception, string context, params string[] tags)
    {
        if (log is null)
        {
            return;
        }

        if (ExceptionLogFormatter.IsBenignDeviceIo(exception))
        {
            log($"{ConnectFlowContext.Prefix()}[{LogTags.Hid}] benign {context}: " +
                $"{exception.GetType().Name}: {exception.Message}");
            return;
        }

        log(ExceptionLogFormatter.FormatHeader(exception, context, MergeTags(tags)));
        foreach (var line in ExceptionLogFormatter.FormatChain(exception))
        {
            log(line);
        }

        foreach (var line in ExceptionLogFormatter.FormatStackTrace(exception))
        {
            log(line);
        }
    }

    private static List<string> MergeTags(params string[] tags)
    {
        var list = new List<string>();
        foreach (var tag in tags)
        {
            if (!string.IsNullOrWhiteSpace(tag))
            {
                list.Add(tag);
            }
        }

        var corr = ConnectFlowContext.CurrentCorrelationId;
        if (!string.IsNullOrWhiteSpace(corr))
        {
            list.Add($"corr={corr}");
        }

        return list;
    }
}
