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
 * NetLogic used to perform the action of accessing a user, which returns the result. 
 */

public class LoginButtonLogic : QPlatform.NetLogic.BaseNetLogic
{
    [ExportMethod]
    public void PerformLogin(string username, string password, out bool loginResult)
    {
        try
        {
            loginResult = ChangeUser(username, password);
        }
        catch (Exception e)
        {
            Log.Error("LoginForm", e.Message);
            loginResult = false;
        }
    }

    private bool ChangeUser(string username, string password)
    {
        var coreCommandsNodeId = QPlatform.CoreBase.Objects.CoreCommands;
        var coreCommandsObject = LogicObject.Context.GetObject(coreCommandsNodeId);
        object[] outputArgs;
        coreCommandsObject.ExecuteMethod("ChangeUser", new object[] { username, password }, out outputArgs);
        return (bool)outputArgs[0];
    }
}
