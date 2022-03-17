#region Using directives
using System;
using QPlatform.Core;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using QPlatform.UI;
using QPlatform.Retentivity;
using QPlatform.CoreBase;
using QPlatform.Report;
using QPlatform.HMIProject;
using QPlatform.Alarm;
using QPlatform.NetLogic;
using QPlatform.OPCUAServer;
using QPlatform.Store;
using QPlatform.SQLiteStore;
using QPlatform.Recipe;
using QPlatform.EventLogger;
#endregion

public class FromToLogic : BaseNetLogic
{
    public override void Start()
    {
        var toVariable = Owner.GetVariable("ToValue");

        if (toVariable == null)
        {
            Log.Error("AlarmHistoryGridWithFilter", "Missing To variable");
            return;
        }

        if (toVariable.Value == null)
        {
            Log.Error("AlarmHistoryGridWithFilter", "Missing To variable value");
            return;
        }

        toVariable.Value = DateTime.UtcNow;

        var fromVariable = Owner.GetVariable("FromValue");

        if (fromVariable == null)
        {
            Log.Error("AlarmHistoryGridWithFilter", "Missing From variable");
            return;
        }

        if (fromVariable.Value == null)
        {
            Log.Error("AlarmHistoryGridWithFilter", "Missing From variable value");
            return;
        }

        fromVariable.Value = DateTime.UtcNow.AddMinutes(-10.0);
        
    }


    public override void Stop()
    {
        // Insert code to be executed when the user-defined logic is stopped
    }
}
