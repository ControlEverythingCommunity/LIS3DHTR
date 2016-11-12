// Copyright (c) Microsoft. All rights reserved.

using System;
using System.Threading;
using Windows.UI.Xaml.Controls;
using Windows.Devices.Enumeration;
using Windows.Devices.I2c;

namespace LIS3DHTR
{
	struct Acceleration
	{
		public double X;
		public double Y;
		public double Z;
	};

	/// <summary>
	/// Sample app that reads data over I2C from an attached LIS3DHTR accelerometer
	/// </summary>
	public sealed partial class MainPage : Page
	{
		private const byte ACCEL_I2C_ADDR = 0x18;			// 7-bit I2C address of the LIS3DHTR
		private const byte ACCEL_REG_CONTROL1 = 0x20;		// Address of the Control register 1
		private const byte ACCEL_REG_CONTROL4 = 0x23;		// Address of the Control register 2
		private const byte ACCEL_REG_X = 0x28;				// Address of the X Axis data register
		private const byte ACCEL_REG_Y = 0x2A;				// Address of the Y Axis data register
		private const byte ACCEL_REG_Z = 0x2C;				// Address of the Z Axis data register

		private I2cDevice I2CAccel;
		private Timer periodicTimer;

		public MainPage()
		{
			this.InitializeComponent();

			// Register for the unloaded event so we can clean up upon exit
			Unloaded += MainPage_Unloaded;

			// Initialize the I2C bus, Accelerometer, and Timer
			InitI2CAccel();
		}

		private async void InitI2CAccel()
		{
			string aqs = I2cDevice.GetDeviceSelector();					// Get a selector string that will return all I2C controllers on the system
			var dis = await DeviceInformation.FindAllAsync(aqs);		// Find the I2C bus controller device with our selector string
			if (dis.Count == 0)
			{
				Text_Status.Text = "No I2C controllers were found on the system";
				return;
			}

			var settings = new I2cConnectionSettings(ACCEL_I2C_ADDR);
			settings.BusSpeed = I2cBusSpeed.FastMode;
			I2CAccel = await I2cDevice.FromIdAsync(dis[0].Id, settings);	// Create an I2cDevice with our selected bus controller and I2C settings
			if (I2CAccel == null)
			{
				Text_Status.Text = string.Format(
					"Slave address {0} on I2C Controller {1} is currently in use by " +
					"another application. Please ensure that no other applications are using I2C.",
					settings.SlaveAddress,
					dis[0].Id);
				return;
			}

			/* 
				Initialize the accelerometer:
				For this device, we create 2-byte write buffers:
				The first byte is the register address we want to write to.
				The second byte is the contents that we want to write to the register. 
			*/
			byte[] WriteBuf_Control1 = new byte[] { ACCEL_REG_CONTROL1, 0x27 };		// 0x27 sets Power ON Mode and Output Data Rate = 10 Hz, X, Y, Z axes enabled
			byte[] WriteBuf_Control4 = new byte[] { ACCEL_REG_CONTROL4, 0x00 };		// 0x00 sets Continuous update and range to +- 2Gs

			// Write the register settings
			try
			{
				I2CAccel.Write(WriteBuf_Control1);
				I2CAccel.Write(WriteBuf_Control4);
			}
			// If the write fails display the error and stop running
			catch (Exception ex)
			{
				Text_Status.Text = "Failed to communicate with device: " + ex.Message;
				return;
			}

			// Now that everything is initialized, create a timer so we read data every 300mS
			periodicTimer = new Timer(this.TimerCallback, null, 0, 300);
		}

		private void MainPage_Unloaded(object sender, object args)
		{
			// Cleanup
			I2CAccel.Dispose();
		}

		private void TimerCallback(object state)
		{
			string xText, yText, zText;
			string addressText, statusText;

			// Read and format accelerometer data
			try
			{
				Acceleration accel = ReadI2CAccel();
				addressText = "I2C Address of the Accelerometer LIS3DHTR: 0x18";
				xText = String.Format("X Axis: {0:F0}", accel.X);
				yText = String.Format("Y Axis: {0:F0}", accel.Y);
				zText = String.Format("Z Axis: {0:F0}", accel.Z);
				statusText = "Status: Running";
			}
			catch (Exception ex)
			{
				xText = "X Axis: Error";
				yText = "Y Axis: Error";
				zText = "Z Axis: Error";
				statusText = "Failed to read from Accelerometer: " + ex.Message;
			}

			// UI updates must be invoked on the UI thread
			var task = this.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
			{
				Text_X_Axis.Text = xText;
				Text_Y_Axis.Text = yText;
				Text_Z_Axis.Text = zText;
				Text_Status.Text = statusText;
			});
		}

		private Acceleration ReadI2CAccel()
		{
			byte[] RegAddrBuf = new byte[] { ACCEL_REG_X };		// Register address we want to read from
			byte[] ReadBuf = new byte[1];						// We read 1 byte to get X-Axis LSB register in one read

			/*
				Read from the accelerometer 
				We call WriteRead() so we write the address of the X-Axis LSB I2C register
			*/
			I2CAccel.WriteRead(RegAddrBuf, ReadBuf);
			
			byte[] RegAddrBuf1 = new byte[] { ACCEL_REG_X + 1 };	// Register address we want to read from
			byte[] ReadBuf1 = new byte[1];							// We read 1 byte to get X-Axis MSB register in one read

			/*
				Read from the accelerometer 
				We call WriteRead() so we write the address of the X-Axis MSB I2C register
			*/
			I2CAccel.WriteRead(RegAddrBuf1, ReadBuf1);

			/*
				In order to get the raw 16-bit data value, we need to concatenate two 8-bit bytes from the I2C read for X-Axis.
			*/
			int AccelRawX = (int)(ReadBuf[0] & 0xFF );
			AccelRawX |= (int)((ReadBuf1[0] & 0xFF) * 256);
			if (AccelRawX > 32767)
			{
				AccelRawX = AccelRawX - 65536;
			}

			byte[] RegAddrBuf2 = new byte[] { ACCEL_REG_Y };	// Register address we want to read from
			byte[] ReadBuf2 = new byte[1];						// We read 1 byte to get Y-Axis LSB register in one read

			/*
				Read from the accelerometer 
				We call WriteRead() so we write the address of the Y-Axis LSB I2C register
			*/
			I2CAccel.WriteRead(RegAddrBuf2, ReadBuf2);
			
			byte[] RegAddrBuf3 = new byte[] { ACCEL_REG_Y + 1 };	// Register address we want to read from
			byte[] ReadBuf3 = new byte[1];							// We read 1 byte to get Y-Axis MSB register in one read

			/*
				Read from the accelerometer
				We call WriteRead() so we write the address of the Y-Axis MSB I2C register
			*/
			I2CAccel.WriteRead(RegAddrBuf3, ReadBuf3);

			/*
				In order to get the raw 16-bit data value, we need to concatenate two 8-bit bytes from the I2C read for Y-Axis.
			*/
			int AccelRawY = (int)(ReadBuf2[0] & 0xFF );
			AccelRawY |= (int)((ReadBuf3[0] & 0xFF) * 256);
			if (AccelRawY > 32767)
			{
				AccelRawY = AccelRawY - 65536;
			}

			byte[] RegAddrBuf4 = new byte[] { ACCEL_REG_Z };	// Register address we want to read from
			byte[] ReadBuf4 = new byte[1];						// We read 1 byte to get Z-Axis LSB register in one read

			/*
				Read from the accelerometer 
				We call WriteRead() so we write the address of the Z-Axis LSB I2C register
			*/
			I2CAccel.WriteRead(RegAddrBuf4, ReadBuf4);
			
			byte[] RegAddrBuf5 = new byte[] { ACCEL_REG_Z + 1 };	// Register address we want to read from
			byte[] ReadBuf5 = new byte[1];							// We read 1 byte to get Z-Axis MSB register in one read

			/*
			Read from the accelerometer 
			We call WriteRead() so we write the address of the Z-Axis MSB I2C register
			*/
			I2CAccel.WriteRead(RegAddrBuf5, ReadBuf5);

			/*
			In order to get the raw 16-bit data value, we need to concatenate two 8-bit bytes from the I2C read for Z-Axis.
			*/
			int AccelRawZ = (int)(ReadBuf4[0] & 0xFF );
			AccelRawZ |= (int)((ReadBuf5[0] & 0xFF) * 256);
			if (AccelRawZ > 32767)
			{
				AccelRawZ = AccelRawZ - 65536;
			}

			Acceleration accel;
			accel.X = AccelRawX;
			accel.Y = AccelRawY;
			accel.Z = AccelRawZ;

			return accel;
		}
	}
}

