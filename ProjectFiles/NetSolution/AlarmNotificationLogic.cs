using System;
using QPlatform.Core;
using CoreBase = QPlatform.CoreBase;
using HMIProject = QPlatform.HMIProject;
using UAManagedCore;
using System.Linq;
using UAManagedCore.Logging;
using QPlatform.Report;
using QPlatform.EventLogger;

/*
 * Library script that notifies in case of alarms and takes care of saving the last active alarm on a model variable.
 */

public class AlarmNotificationLogic : QPlatform.NetLogic.BaseNetLogic
{
	public override void Start()
	{
		IContext context = LogicObject.Context;

		affinityId = context.AssignAffinityId();

		RegisterObserverOnLocalizedAlarmsContainer(context);
		RegisterObserverOnSessionLocaleIdChanged(context);
		RegisterObserverOnLocalizedAlarmsObject(context);
	}

	public override void Stop()
	{
		if (alarmEventRegistration != null)
			alarmEventRegistration.Dispose();
		if (alarmEventRegistration2 != null)
			alarmEventRegistration2.Dispose();

		alarmEventRegistration = null;
		alarmEventRegistration2 = null;
		alarmsNotificationObserver = null;
		retainedAlarmsObjectObserver = null;
	}

	public void RegisterObserverOnLocalizedAlarmsObject(IContext context)
	{
		var retainedAlarms = context.GetNode(QPlatform.Alarm.Objects.RetainedAlarms);

		retainedAlarmsObjectObserver = new RetainedAlarmsObjectObserver((ctx) => RegisterObserverOnLocalizedAlarmsContainer(ctx));

		// observe ReferenceAdded of localized alarm containers
		alarmEventRegistration2 = retainedAlarms.RegisterEventObserver(
			retainedAlarmsObjectObserver, EventType.ForwardReferenceAdded, affinityId);
	}

	public void RegisterObserverOnLocalizedAlarmsContainer(IContext context)
	{
		var retainedAlarms = context.GetNode(QPlatform.Alarm.Objects.RetainedAlarms);
		var localizedAlarmsVariable = retainedAlarms.Children.Get<IUAVariable>("LocalizedAlarms");
		var localizedAlarmsContainer = context.GetNode((NodeId)localizedAlarmsVariable.GetValue());

		if (alarmEventRegistration != null)
		{
			alarmEventRegistration.Dispose();
			alarmEventRegistration = null;
		}

		alarmsNotificationObserver = new AlarmsNotificationObserver(LogicObject);
		alarmsNotificationObserver.Initialize();

		alarmEventRegistration = localizedAlarmsContainer.RegisterEventObserver(
			alarmsNotificationObserver,
			EventType.ForwardReferenceAdded | EventType.ForwardReferenceRemoved, affinityId);
	}

	public void RegisterObserverOnSessionLocaleIdChanged(IContext context)
	{
		var currentSessionLocaleIds = context.Sessions.CurrentSessionInfo.SessionObject.Children["ActualLocaleIds"];

		localeIdChangedObserver = new CallbackVariableChangeObserver((IUAVariable variable, UAValue newValue, UAValue oldValue, uint[] a, ulong aa) =>
		{
			RegisterObserverOnLocalizedAlarmsContainer(context);
		});

		localeIdsRegistration = currentSessionLocaleIds.RegisterEventObserver(
			localeIdChangedObserver, EventType.VariableValueChanged, affinityId);
	}

	uint affinityId = 0;
	AlarmsNotificationObserver alarmsNotificationObserver;
	RetainedAlarmsObjectObserver retainedAlarmsObjectObserver;
	IEventRegistration alarmEventRegistration;
	IEventRegistration alarmEventRegistration2;
	IEventRegistration localeIdsRegistration;
	IEventObserver localeIdChangedObserver;
}

public class RetainedAlarmsObjectObserver : IReferenceObserver
{
	public RetainedAlarmsObjectObserver(Action<IContext> action)
	{
		registrationCallback = action;
	}

	public void OnReferenceAdded(IUANode sourceNode, IUANode targetNode, NodeId referenceTypeId, ulong senderId)
	{
		string localeId = "en-US";

		var localeIds = targetNode.Context.Sessions.CurrentSessionHandler.ActualLocaleIds;
		if (localeIds.Count > 0)
			localeId = localeIds.First();

		targetNode.Context.Sessions.CurrentSessionHandler.ActualLocaleIds.First();

		if (targetNode.BrowseName == localeId)
			registrationCallback(targetNode.Context);
	}

	public void OnReferenceRemoved(IUANode sourceNode, IUANode targetNode, NodeId referenceTypeId, ulong senderId)
	{

	}

	private Action<IContext> registrationCallback;
}

public class AlarmsNotificationObserver : IReferenceObserver
{
	public AlarmsNotificationObserver(IUANode LogicObject)
	{
		this.LogicObject = LogicObject;
	}

	public void Initialize()
	{
		retainedAlarmsCount = LogicObject.Children.Get<IUAVariable>("AlarmCount");
		lastAlarm = LogicObject.Children.Get<IUAVariable>("LastAlarm");

		IContext context = LogicObject.Context;
		var retainedAlarms = context.GetNode(QPlatform.Alarm.Objects.RetainedAlarms);
		var localizedAlarmsVariable = retainedAlarms.Children.Get<IUAVariable>("LocalizedAlarms");
        var localizedAlarmsNodeId = (NodeId)localizedAlarmsVariable.Value;
        IUANode localizedAlarmsContainer = null;
        if (localizedAlarmsNodeId != null && !localizedAlarmsNodeId.IsEmpty)
            localizedAlarmsContainer = context.GetNode(localizedAlarmsNodeId);

        retainedAlarmsCount.Value = localizedAlarmsContainer?.Children.Count ?? 0;
        if (retainedAlarmsCount.Value > 0)
		{
			lastAlarm.Value = localizedAlarmsContainer.Children.Last().NodeId;
		}
		else
		{
			lastAlarm.Value = NodeId.Empty;
		}
	}

	public void OnReferenceAdded(IUANode sourceNode, IUANode targetNode, NodeId referenceTypeId, ulong senderId)
	{
		++retainedAlarmsCount.Value;

		lastAlarm.Value = targetNode.NodeId;
	}

	public void OnReferenceRemoved(IUANode sourceNode, IUANode targetNode, NodeId referenceTypeId, ulong senderId)
	{
        if (retainedAlarmsCount.Value > 0)
            --retainedAlarmsCount.Value;

		IContext context = LogicObject.Context;
		var retainedAlarms = context.GetNode(QPlatform.Alarm.Objects.RetainedAlarms);
		var localizedAlarmsVariable = retainedAlarms.Children.Get<IUAVariable>("LocalizedAlarms");
        var localizedAlarmsNodeId = (NodeId)localizedAlarmsVariable.Value;
        IUANode localizedAlarmsContainer = null;
        if (localizedAlarmsNodeId != null && !localizedAlarmsNodeId.IsEmpty)
            localizedAlarmsContainer = context.GetNode(localizedAlarmsNodeId);

        if (retainedAlarmsCount.Value == 0 || localizedAlarmsContainer == null)
        {
			lastAlarm.Value = NodeId.Empty;
		}
		else
		{
            lastAlarm.Value = localizedAlarmsContainer.Children.Last().NodeId;
		}
	}

	private IUAVariable retainedAlarmsCount;
	private IUAVariable lastAlarm;
	private IUANode LogicObject;
}
