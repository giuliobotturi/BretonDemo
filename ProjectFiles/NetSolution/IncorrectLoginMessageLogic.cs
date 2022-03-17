#region StandardUsing
using System;
using QPlatform.Core;
using QPlatform.CoreBase;
using QPlatform.HMIProject;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using QPlatform.NetLogic;
using QPlatform.UI;
using QPlatform.Store;
using QPlatform.SQLiteStore;
using QPlatform.Retentivity;
using QPlatform.Recipe;
using QPlatform.OPCUAServer;
using QPlatform.Alarm;
using QPlatform.Report;
using QPlatform.EventLogger;
#endregion

/*
 * Logic that displays an error message for 5 seconds in case of incorrect login.
 */

public class IncorrectLoginMessageLogic : QPlatform.NetLogic.BaseNetLogic
{
    public override void Start()
    {
        loginPopup = (Popup)Owner.Owner.Owner;
        incorrectLoginMessageLabel = (QPlatform.UI.Label)Owner;

        task = new DelayedTask(() =>
        {
            incorrectLoginMessageLabel.Visible = false;
        }, 5000, LogicObject);
    }

    public override void Stop()
    {
        task?.Dispose();
    }

    [ExportMethod]
    public void DecideActionBasedOnLoginResult(bool loginSucceded)
    {
        if (loginSucceded)
            loginPopup.Close();
        else
            ViewIncorrectLoginMessage();
    }

    private void ViewIncorrectLoginMessage()
    {
        incorrectLoginMessageLabel.Visible = true;

        if (taskStarted)
        {
            task?.Cancel();
            taskStarted = false;
        }

        task.Start();
        taskStarted = true;
    }

    DelayedTask task;
    bool taskStarted = false;
    QPlatform.UI.Label incorrectLoginMessageLabel;
    Popup loginPopup;
}
