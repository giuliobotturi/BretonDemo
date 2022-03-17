#region StandardUsing
using System;
using QPlatform.Core;
using QPlatform.CoreBase;
using QPlatform.HMIProject;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using QPlatform.NetLogic;
using QPlatform.OPCUAServer;
using QPlatform.UI;
using QPlatform.Report;
using QPlatform.EventLogger;
#endregion

/*
 * NetLogic to perform the deletion of a user through user editor.
 */

public class DeleteUserButtonLogic : QPlatform.NetLogic.BaseNetLogic
{
    [ExportMethod]
    public void DeleteUser(NodeId userToDelete)
    {
        var userObjectToRemove = Owner.Context.GetNode(userToDelete);
        if (userObjectToRemove == null)
        {
            Log.Error("UserEditor", "Cannot obtain the selected user.");
            return;
        }

        var username = userObjectToRemove.BrowseName;
        if (username == "translator" || 
            username == "admin" || 
            username == "Guest" || 
            username == "Asem")
        {
            Log.Error("User " + username + " cannot be removed");
            return;
        }

        var userVariable = Owner.Owner.Owner.Owner.GetVariable("Users");
        if (userVariable == null)
        {
            Log.Error("UserEditor", "Missing user variable in UserEditor Panel.");
            return;
        }

        if (userVariable.Value == null || (NodeId)userVariable.Value == NodeId.Empty)
        {
            Log.Error("UserEditor", "Fill User variable in UserEditor.");
            return;
        }
        var userFolder = Owner.Context.GetNode(userVariable.Value);
        if (userFolder == null)
        {
            Log.Error("UserEditor", "Cannot obtain Users folder.");
            return;
        }


        userFolder.Remove(userObjectToRemove);
    }
}
