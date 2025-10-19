# WPF Modbus RTU Implementation Guide

## ✅ What's Been Implemented

### 1. Core Services
- **ModbusService.cs** (`/Services/ModbusService.cs`)
  - Full Modbus RTU protocol implementation
  - Function Code 03: Read Holding Registers
  - Function Code 05: Force Single Coil (for commands)
  - Function Code 06: Preset Single Register
  - Function Code 16: Preset Multiple Registers
  - 16-bit CRC calculation and validation
  - Serial port management (RS485)
  - Event-driven error handling

### 2. Data Models
- **DeviceState.cs** (`/Models/DeviceState.cs`)
  - Position, Torque tracking
  - Status bits (PowerOK, Communication, Calibrated, Moving, Open/Close Limits)
  - Current status tracking

- **ActuatorConfiguration.cs** (`/Models/ActuatorConfiguration.cs`)
  - All configuration parameters (Modbus address, speed, torque, positions, etc.)
  - JSON serialization support for save/load

### 3. Page 1 - Connection (COMPLETE)
- ✅ COM port selection and configuration
- ✅ Baud rate, parity, stop bits selection
- ✅ Modbus address configuration (1-254)
- ✅ Open/Close port functionality
- ✅ Connection status display with color-coded messages
- ✅ Input validation
- ✅ Error handling

### 4. Page 2 - Control (COMPLETE)
- ✅ Automatic polling (500ms interval)
- ✅ Position display with animated bar
- ✅ Torque display with color-coded bar (green/yellow/red)
- ✅ Status badge (STOPPED/OPENING/CLOSING/OPEN/CLOSED)
- ✅ Open command button (sends Modbus coil 0)
- ✅ Close command button (sends Modbus coil 1)
- ✅ Stop command button (sends Modbus coil 2)
- ✅ Status bits display
- ✅ Real-time UI updates

### 5. Application State
- **App.xaml.cs**
  - Singleton ModbusService shared across all pages
  - Singleton DeviceState for real-time data
  - Global ActuatorConfiguration
  - Proper disposal on exit

## 📝 What Still Needs Implementation

### Page 3 - Configuration (TODO)
You'll need to implement:
```csharp
// Page3.xaml.cs

private void EnterSetupModeButton_Click(object sender, RoutedEventArgs e)
{
    // Check if device is in Stop Mode (read status register)
    // Send "Soft Setup Cmd" to put device in Setup Mode
    // Use WriteSingleCoil or WriteSingleRegister
}

private void ExitSetupModeButton_Click(object sender, RoutedEventArgs e)
{
    // Clear "Soft Setup Cmd" to exit Setup Mode
}

private void SaveToFileButton_Click(object sender, RoutedEventArgs e)
{
    var config = App.CurrentConfiguration;
    string json = System.Text.Json.JsonSerializer.Serialize(config);

    var saveDialog = new Microsoft.Win32.SaveFileDialog();
    saveDialog.Filter = "JSON files (*.json)|*.json";
    saveDialog.DefaultExt = ".json";

    if (saveDialog.ShowDialog() == true)
    {
        System.IO.File.WriteAllText(saveDialog.FileName, json);
    }
}

private void LoadFromFileButton_Click(object sender, RoutedEventArgs e)
{
    var openDialog = new Microsoft.Win32.OpenFileDialog();
    openDialog.Filter = "JSON files (*.json)|*.json";

    if (openDialog.ShowDialog() == true)
    {
        string json = System.IO.File.ReadAllText(openDialog.FileName);
        var config = System.Text.Json.JsonSerializer.Deserialize<ActuatorConfiguration>(json);
        App.CurrentConfiguration = config;
        // Update UI with loaded values
    }
}

private void WriteToDeviceButton_Click(object sender, RoutedEventArgs e)
{
    if (!IsInSetupMode())
    {
        MessageBox.Show("Device must be in Setup Mode to write configuration");
        return;
    }

    // Write config to Modbus registers 11, 101-111
    // Use WriteMultipleRegisters function
    ushort[] configData = ConvertConfigToRegisters(App.CurrentConfiguration);
    _modbusService.WriteMultipleRegisters(11, configData);
}
```

### Page 4 - Network (TODO)
Multi-device polling:
```csharp
// Create a polling algorithm that:
// 1. Parses custom sequence textbox
// 2. Polls each device in sequence
// 3. Reads register 100 for Product Identifier
// 4. Displays devices with colors
// 5. Shows connection status
```

## 🔧 Modbus Register Map (EXAMPLE - Adjust to Your Actuator)

```
Register  | Description
----------|------------------
0         | Position (0-1000 = 0-100%)
1         | Torque (0-1000 = 0-100%)
2         | Status Word (bit field)
11        | Modbus Address
100       | Product Identifier
101-111   | Configuration Data
```

### Status Word Bit Fields (Register 2)
```
Bit 0: Power OK
Bit 1: Communication OK
Bit 2: Calibrated
Bit 3: Moving
Bit 4: Open Limit
Bit 5: Close Limit
```

### Coil Addresses (for commands)
```
Coil 0: Open Command
Coil 1: Close Command
Coil 2: Stop Command
```

## 🚀 How to Test Without Hardware

1. **Use a Modbus Simulator**
   - Install ModRSsim2 or similar
   - Configure it to respond on a virtual COM port
   - Set up registers with test data

2. **Mock Testing**
   - Comment out actual Modbus calls
   - Return fake data for testing UI

## 📦 Required NuGet Packages

None! The implementation uses only built-in .NET libraries:
- `System.IO.Ports` (for serial communication)
- `System.Text.Json` (for config save/load)

## 🎯 Key Features Implemented

### Requirements Met:
- ✅ 5.1 Connection Page - ALL requirements
- ✅ 5.2 Control Page - ALL requirements
- ✅ 5.6 Network Protocol - Modbus RTU with CRC
- ✅ Function Codes 03, 05, 06, 16
- ✅ Polling algorithm
- ✅ Visual indicators
- ✅ Command buttons
- ✅ Error handling and CRC validation

### Still To Implement:
- ⏳ 5.3 Configuration/Calibration Page (save/load/write)
- ⏳ 5.5 Network Page (multi-device polling)
- ⏳ Setup Mode management
- ⏳ Slider event handlers for speed/torque

## 💡 Tips for Completion

1. **Wire up Configuration Page sliders:**
```csharp
SpeedSlider.ValueChanged += (s, e) =>
{
    SpeedValueText.Text = $"{SpeedSlider.Value}%";
    App.CurrentConfiguration.SpeedSetting = (int)SpeedSlider.Value;
};
```

2. **Implement proper Setup Mode detection:**
```csharp
private bool IsInSetupMode()
{
    ushort[] registers = _modbusService.ReadHoldingRegisters(SETUP_MODE_REGISTER, 1);
    return registers != null && (registers[0] & 0x0001) != 0;
}
```

3. **Add data binding for easier UI updates:**
   - Consider using INotifyPropertyChanged in DeviceState
   - Bind XAML properties directly to state objects

## 🐛 Common Issues & Solutions

1. **"COM port not found"**
   - Check Device Manager for available ports
   - Ensure USB-to-RS485 adapter drivers are installed

2. **CRC errors**
   - Verify baud rate matches device
   - Check parity and stop bits settings
   - Ensure proper RS485 termination

3. **No response from device**
   - Verify Modbus address is correct
   - Check physical wiring (A, B, GND)
   - Increase read timeout in ModbusService

## 📚 Additional Resources

- Modbus Protocol Specification: https://modbus.org/docs/Modbus_Application_Protocol_V1_1b3.pdf
- RS485 Wiring Guide: Ensure proper A/B connections and termination
- Bray S7X Actuator Manual: Reference for exact register map

---

**Next Steps:**
1. Complete Page3.xaml.cs with file operations and setup mode
2. Complete Page4.xaml.cs with network polling
3. Test with Modbus simulator
4. Connect to actual hardware
5. Fine-tune register addresses based on actual device

