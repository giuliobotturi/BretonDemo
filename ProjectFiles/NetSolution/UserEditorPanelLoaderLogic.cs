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
using QPlatform.Retentivity;
using QPlatform.Report;
using QPlatform.EventLogger;
#endregion

/*
 * NetLogic used by the user editor to display a page given the number of existing users.
 * If there is at least one user, the UserDetailPanel is shown, otherwise the NoUserDetailPanel.
 */

public class UserEditorPanelLoaderLogic : QPlatform.NetLogic.BaseNetLogic
{
    public override void Start()
    {
    }

    public override void Stop()
    {
    }

	[ExportMethod]
	public void GoToUserDetailsPanel()
	{
		var userCountVariable = LogicObject.Get<IUAVariable>("UserCount");
		if (userCountVariable == null)
			return;

		var noUsersPanelVariable = LogicObject.Get<IUAVariable>("NoUsersPanel");
		if (noUsersPanelVariable == null)
			return;

		var userDetailPanelVariable = LogicObject.Get<IUAVariable>("UserDetailPanel");
		if (userDetailPanelVariable == null)
			return;

		var panelLoader = (PanelLoader)Owner;

		NodeId newPanelNodeId = userCountVariable.Value > 0 ? userDetailPanelVariable.Value : noUsersPanelVariable.Value;
		panelLoader.ChangePanel(newPanelNodeId, NodeId.Empty);
	}
}
