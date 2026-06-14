using System;
using System.Collections.Generic;

namespace MigrationDashboard.Models;

public static class DisasterState
{
    public static bool IsSingaporeDown { get; set; } = false;
    public static bool IsFailedOver { get; set; } = false;
    public static string CurrentRegion { get; set; } = "ap-southeast-1"; // ap-southeast-1 or ap-northeast-1
    public static List<string> FailoverLogs { get; set; } = new();
    public static double RecoveryTimeSeconds { get; set; } = 0;
    public static DateTime? FailoverStartTime { get; set; }
    public static DateTime? FailoverEndTime { get; set; }
    public static string FailoverStatus { get; set; } = "STANDBY"; // STANDBY, RUNNING, COMPLETED, FAILED

    public static void Reset()
    {
        IsSingaporeDown = false;
        IsFailedOver = false;
        CurrentRegion = "ap-southeast-1";
        FailoverLogs.Clear();
        RecoveryTimeSeconds = 0;
        FailoverStartTime = null;
        FailoverEndTime = null;
        FailoverStatus = "STANDBY";
    }

    public static void AddLog(string message)
    {
        var timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        FailoverLogs.Add($"[{timestamp}] {message}");
    }
}
