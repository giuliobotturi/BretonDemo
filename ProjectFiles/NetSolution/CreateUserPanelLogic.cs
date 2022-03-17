#region Using directives
using System;
using QPlatform.Core;
using QPlatform.CoreBase;
using QPlatform.HMIProject;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using QPlatform.NetLogic;
using QPlatform.OPCUAServer;
using QPlatform.UI;
#endregion

public class CreateUserPanelLogic : BaseNetLogic
{
    [ExportMethod]
    public void CreateUser(string username, string password, string locale, out NodeId result)
    {
		result = NodeId.Empty;
		if (String.IsNullOrEmpty(username))
		{
			Log.Error("CreateUserPanelLogic", "Cannot create user with empty username");
			return;
		}

		result = GenerateUser(username, password, locale);
    }

    private NodeId GenerateUser(string username, string password, string locale)
    {
        var user = InformationModel.MakeObject<QPlatform.Core.User>(username);
        user.Password = password;
        user.LocaleIds = new string[] { locale };

		var users = GetUsers();
		if (users == null)
		{
			Log.Error("CreateUserPanelLogic", "Unable to get users");
			return NodeId.Empty;
		}

		users.Add(user);
		return user.NodeId;
	}

    private IUANode GetUsers()
	{
		var pathResolverResult = LogicObject.Context.ResolvePath(LogicObject, "{Users}");
		if (pathResolverResult == null)
			return null;
		if (pathResolverResult.ResolvedNode == null)
			return null;

		return pathResolverResult.ResolvedNode;
	}
}
