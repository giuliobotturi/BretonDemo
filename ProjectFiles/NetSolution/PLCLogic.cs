#region StandardUsing
using System;
using QPlatform.Core;
using System.Linq;
using QPlatform.CoreBase;
using QPlatform.HMIProject;
using UAManagedCore;
using OpcUa = UAManagedCore.OpcUa;
using QPlatform.NetLogic;
using QPlatform.UI;
using QPlatform.OPCUAServer;
using System.Collections.Generic;
using System.Threading;
using QPlatform.Report;
using QPlatform.EventLogger;
#endregion

/*
 * NetLogic that simulates a PLC.
 */

public class PLCLogic : QPlatform.NetLogic.BaseNetLogic
{
	public override void Start()
	{
		polishingMachine = (PolishingMachine)Owner.GetObject("PolishingMachine");
		slabsNode = Owner.GetObject("Slabs");

		clock = new Clock();
		CollectSpindles();
		CollectSlabs();

		periodicTask = new PeriodicTask(PLCTask, 50, polishingMachine.Spindles);
		periodicTask.Start();
		plcLogicRunning = true;
	}

	public override void Stop()
	{
		periodicTask.Cancel();
		plcLogicRunning = false;
	}

	[ExportMethod]
	public void StopPLC()
	{
		lock (lockObject)
		{
			if (!plcLogicRunning)
				return;

			periodicTask.Cancel();
			plcLogicRunning = false;
		}
	}

	[ExportMethod]
	public void StartPLC()
	{
		lock (lockObject)
		{
			if (plcLogicRunning)
				return;

			CollectSpindles();
			CollectSlabs();

			periodicTask.Start();
			plcLogicRunning = true;
		}
	}

	private void CollectSpindles()
	{
		spindles = new List<SpindleModel>();

		foreach (var spindle in polishingMachine.Spindles.GetObject("SpindlesContainer").Children.OfType<Spindle>())
		{
			var spindlePlc = new SpindleModel(spindle, clock);
			spindles.Add(spindlePlc);
		}
	}

	private void CollectSlabs()
	{
		slabs = new List<SlabModel>();
		foreach (var slabNode in slabsNode.Children.OfType<Slab>())
		{
			var slabPlc = new SlabModel(slabNode, clock);
			slabs.Add(slabPlc);
		}
		beam = new BeamModel(polishingMachine, clock);

		spindlesUpdater = new SpindleUpdater(slabs);
		var beltSpeed = slabsNode.GetVariable("BeltSpeed");
		slabUpdater = new SlabUpdater(polishingMachine.Width, beltSpeed.Value);
		beamUpdater = new BeamUpdater();
		beltSpeed.VariableChange += BeltSpeed_VariableChange;
	}

	private void BeltSpeed_VariableChange(object sender, VariableChangeEventArgs e)
	{
		slabUpdater.OnBeltSpeedChanged(e.NewValue);
	}

	private void PLCTask()
	{
		clock.Update();

		foreach (var spindle in spindles)
			spindlesUpdater.Update(spindle);

		foreach (var slab in slabs)
			slabUpdater.Update(slab);

		beamUpdater.Update(beam);
	}

	class ModelVariable<T>
	{
		public ModelVariable(IUAVariable variable, int updatePeriodMs, Clock clock)
		{
			this.value = variable.Value.Value;
			this.updatePeriodMs = updatePeriodMs;
			this.variable = variable;
			this.lastWrittenValue = value;
			this.clock = clock;
		}

		public T Value
		{
			get => (T) value;
			set
			{
				this.value = value;

				if (lastWrittenValue.Equals(value))
					return;

				var currentTime = clock.CurrentTime;
				if (currentTime >= lastUpdateTime + updatePeriodMs)
				{
					lastUpdateTime = currentTime;
					variable.SetValue(value);
					lastWrittenValue = value;
				}
			}
		}

		private int updatePeriodMs;
		private long lastUpdateTime = 0;
		private object value;
		private object lastWrittenValue;
		IUAVariable variable;
		Clock clock;
	}

	class SpindleModel
	{
		public SpindleModel(Spindle spindle, Clock clock)
		{
			this.Id = spindle.Get<IUAVariable>("ID").Value;
			this.active = new ModelVariable<bool>(spindle.Configuration.GetVariable("Active"), 100, clock);
			this.input = new ModelVariable<float>(spindle.Process.GetVariable("Input"), 1000, clock);
			this.pressure = new ModelVariable<float>(spindle.Process.GetVariable("Pressure"), 500, clock);
			this.abrasiveLevel = new ModelVariable<float>(spindle.Process.GetVariable("AbrasiveLevel"), 500, clock);
		}

		public bool Active { get => active.Value; set => active.Value = value; }
		public float Input { get => input.Value; set => input.Value = value; }
		public float Pressure { get => pressure.Value; set => pressure.Value = value; }
		public float AbrasiveLevel { get => abrasiveLevel.Value; set => abrasiveLevel.Value = value; }

		public int Id;

		private ModelVariable<bool> active;
		private ModelVariable<float> input;
		private ModelVariable<float> pressure;
		private ModelVariable<float> abrasiveLevel;
	}

	class SpindleUpdater
	{
		public SpindleUpdater(List<SlabModel> slabs)
		{
			this.slabs = slabs;
		}

		public void Update(SpindleModel spindle)
		{
			spindle.Active = GetNextActiveValue(spindle.Id);
			spindle.AbrasiveLevel = GetNexAbrasiveLevelValue(spindle.AbrasiveLevel, spindle.Active);
			if(spindle.Active)
			{
				spindle.Pressure = GetNextPressureValue();
				spindle.Input = GetNextInputValue();
			}
		}

		private bool GetNextActiveValue(int id)
		{
			var spindleX = id * 52;
			foreach (var slab in slabs)
				if (slab.IsUnder(spindleX))
					return true;
			return false;
		}

		private float GetNexAbrasiveLevelValue(float currentValue, bool descending)
		{
			var downStep = 0.06f;
			var upStep = 0.06f;
			if (descending && (currentValue - downStep) >= spindleAbrasiveLevelMin)
				currentValue -= downStep;
			else if ((currentValue + upStep) <= spindleAbrasiveLevelMax)
				currentValue += upStep;
			return currentValue;
		}

		private float GetNextInputValue()
		{
			return GetRandomFloat(spindleInputMin, spindleInputMax);
		}

		private float GetNextPressureValue()
		{
			return GetRandomFloat(spindlePressureMin, spindlePressureMax);
		}

		private bool RandomBool(float trueProb)
		{
			return random.Next(100) < (100 * trueProb);
		}

		private float GetRandomFloat(float min, float max)
		{
			return (float)(random.NextDouble() * (max - min) + min);
		}

		private List<SlabModel> slabs;
		private Random random = new Random();

		private const float spindleAbrasiveLevelMin = 0;
		private const float spindleAbrasiveLevelMax = 10;
		private const float spindleInputMin = 0;
		private const float spindleInputMax = 30;
		private const float spindlePressureMin = 1;
		private const float spindlePressureMax = 3;
	}

	class SlabModel
	{
		public SlabModel(Slab slab, Clock clock)
		{
			this.position = new ModelVariable<float>(slab.PositionVariable, 100, clock);
			this.Width = slab.Width;
		}

		public float Position { get => position.Value; set => position.Value = value; }

		public bool IsUnder(float x)
		{
			var slabStart = position.Value;
			var slabEnd = slabStart + Width;
			return x + 52 > slabStart && x < slabEnd;
		}

		public float Width;

		private ModelVariable<float> position;
	}

	class SlabUpdater
	{
		public SlabUpdater(float machineWidth, float beltSpeed)
		{
			this.machineWidth = machineWidth;
			this.beltSpeed = beltSpeed;
		}

		public void OnBeltSpeedChanged(float newValue)
		{
			beltSpeed = newValue;
		}

		public void Update(SlabModel slab)
		{
			slab.Position = GetNextPosition(slab.Position, slab.Width);
		}

		private float GetNextPosition(float currentValue, float slabWidth)
		{
			if (currentValue < machineWidth)
				currentValue += beltSpeed;
			else
				currentValue = -slabWidth - 150;
			return currentValue;
		}

		private float machineWidth;
		private float beltSpeed;
	}

	class BeamModel
	{
		public BeamModel(PolishingMachine polishingMachine, Clock clock)
		{
			this.bottomOut = new ModelVariable<bool>(polishingMachine.SpindlesBeam.BottomOutVariable, 1000, clock);
			this.position = new ModelVariable<float>(polishingMachine.SpindlesBeam.PositionVariable, 100, clock);
		}

		public bool BottomOut { get => bottomOut.Value; set => bottomOut.Value = value; }
		public float Position { get => position.Value; set => position.Value = value; }

		private ModelVariable<bool> bottomOut;
		private ModelVariable<float> position;
	}

	class BeamUpdater
	{
		public void Update(BeamModel beam)
		{
			beam.BottomOut = GetNextBottomOut(beam.BottomOut);
			beam.Position = GetNextPosition(beam.Position, beam.BottomOut);
		}

		private bool GetNextBottomOut(bool currentValue)
		{
			currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();

			if (currentTime >= lastUpdateTime + beamTransationTimeMs)
			{
				lastUpdateTime = currentTime;
				return !currentValue;
			}
			return currentValue;
		}

		private float GetNextPosition(float currentValue, bool descending)
		{
			if (descending)
				currentValue -= 5f;
			else
				currentValue += 5f;
			return currentValue;
		}

		private long currentTime;
		private long lastUpdateTime = 0;
		private const int beamTransationTimeMs = 2000;
	}

	class Clock
	{
		public void Update()
		{
			currentTime = DateTimeOffset.Now.ToUnixTimeMilliseconds();
		}

		private long currentTime;

		public long CurrentTime { get => currentTime; }
	}

	Clock clock;
	private object lockObject = new object();
	private bool plcLogicRunning = false;

	private const int updatePeriod = 250;
	private const int basePeriod = 50;

	private IUANode slabsNode;
	private PolishingMachine polishingMachine;
	private PeriodicTask periodicTask;

	private List<SpindleModel> spindles;
	private SpindleUpdater spindlesUpdater;

	private List<SlabModel> slabs;
	private SlabUpdater slabUpdater;

	private BeamModel beam;
	private BeamUpdater beamUpdater;
}
