좋다 👍
그럼 **SNMPc 콘솔의 핵심 두 축을 동시에 완성**하자.

> ✅ **MapView 고도화 (장비/링크/상태/확대/드래그)**
> ✅ **Event 처리 파이프라인 → Map 상태 실시간 반영**

아래는 **“실제로 돌아가는 구조 + 이후 SNMP 연동까지 그대로 확장 가능한 설계”**다.

---

# 🔥 3단계 구현

## ① MapView 고도화 + ② Event → Map 연동

---

## PART 1️⃣ MapView 고도화 (진짜 NMS 맵)

---

## 1. Map에서 다룰 핵심 객체 모델

### 📌 DeviceModel.cs

```csharp
using System.ComponentModel;

public enum DeviceStatus
{
    Up,
    Down,
    Warning
}

public class DeviceModel : INotifyPropertyChanged
{
    public string Name { get; set; }
    public double X { get; set; }
    public double Y { get; set; }

    private DeviceStatus _status;
    public DeviceStatus Status
    {
        get => _status;
        set
        {
            _status = value;
            PropertyChanged?.Invoke(this,
                new PropertyChangedEventArgs(nameof(Status)));
        }
    }

    public event PropertyChangedEventHandler PropertyChanged;
}
```

---

## 2. MapViewModel (장비 컬렉션)

```csharp
using System.Collections.ObjectModel;

public class MapViewModel
{
    public ObservableCollection<DeviceModel> Devices { get; }
        = new ObservableCollection<DeviceModel>();

    public MapViewModel()
    {
        // 테스트 장비
        Devices.Add(new DeviceModel
        {
            Name = "Router-1",
            X = 100,
            Y = 100,
            Status = DeviceStatus.Up
        });

        Devices.Add(new DeviceModel
        {
            Name = "Switch-1",
            X = 300,
            Y = 200,
            Status = DeviceStatus.Up
        });
    }
}
```

---

## 3. MapView.xaml (Canvas + 상태 바인딩)

```xml
<UserControl x:Class="NmsClient.Views.MapView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Canvas Background="#EEE">

        <ItemsControl ItemsSource="{Binding Devices}">
            <ItemsControl.ItemsPanel>
                <ItemsPanelTemplate>
                    <Canvas/>
                </ItemsPanelTemplate>
            </ItemsControl.ItemsPanel>

            <ItemsControl.ItemTemplate>
                <DataTemplate>
                    <Grid Canvas.Left="{Binding X}"
                          Canvas.Top="{Binding Y}">

                        <!-- Device Icon -->
                        <Ellipse Width="40" Height="40">
                            <Ellipse.Style>
                                <Style TargetType="Ellipse">
                                    <Setter Property="Fill" Value="Green"/>
                                    <Style.Triggers>
                                        <DataTrigger Binding="{Binding Status}" Value="Down">
                                            <Setter Property="Fill" Value="Red"/>
                                        </DataTrigger>
                                        <DataTrigger Binding="{Binding Status}" Value="Warning">
                                            <Setter Property="Fill" Value="Orange"/>
                                        </DataTrigger>
                                    </Style.Triggers>
                                </Style>
                            </Ellipse.Style>
                        </Ellipse>

                        <!-- Label -->
                        <TextBlock Text="{Binding Name}"
                                   Margin="-10,45,0,0"/>
                    </Grid>
                </DataTemplate>
            </ItemsControl.ItemTemplate>

        </ItemsControl>

    </Canvas>
</UserControl>
```

---

## 4. MapView.xaml.cs

```csharp
public partial class MapView : UserControl
{
    public MapViewModel ViewModel { get; }

    public MapView()
    {
        InitializeComponent();
        ViewModel = new MapViewModel();
        DataContext = ViewModel;
    }
}
```

📌 여기까지 하면
✔ 여러 장비 표시
✔ 상태별 색상 자동 변경
✔ SNMPc Map과 동일한 개념 완성

---

## PART 2️⃣ Event 처리 파이프라인 (핵심)

---

## 5. Event 모델 정의

```csharp
public class NmsEvent
{
    public string DeviceName { get; set; }
    public string Severity { get; set; } // Critical, Major
    public string Message { get; set; }
}
```

---

## 6. EventBus (중앙 이벤트 허브)

📌 **SNMPc Server 역할**

```csharp
using System;

public static class EventBus
{
    public static event Action<NmsEvent> OnEventReceived;

    public static void Publish(NmsEvent evt)
    {
        OnEventReceived?.Invoke(evt);
    }
}
```

---

## 7. MapViewModel ← Event 수신

```csharp
public MapViewModel()
{
    Devices.Add(new DeviceModel
    {
        Name = "Router-1",
        X = 100,
        Y = 100,
        Status = DeviceStatus.Up
    });

    EventBus.OnEventReceived += HandleEvent;
}

private void HandleEvent(NmsEvent evt)
{
    var device = Devices
        .FirstOrDefault(d => d.Name == evt.DeviceName);

    if (device == null) return;

    if (evt.Severity == "Critical")
        device.Status = DeviceStatus.Down;
    else if (evt.Severity == "Major")
        device.Status = DeviceStatus.Warning;
}
```

📌 **이 순간 Map 색상이 즉시 바뀐다**

---

## 8. Event 발생 테스트 (임시)

```csharp
EventBus.Publish(new NmsEvent
{
    DeviceName = "Router-1",
    Severity = "Critical",
    Message = "SNMP Timeout"
});
```

👉 이 코드를 아무 버튼이나 Timer에 붙이면
**Map이 실시간으로 변한다**

---

## 9. EventLogView와 연동 구조

이제 EventLogView는:

* EventBus 구독
* DataGrid에 Event 추가
* 더블클릭 → MapView 해당 장비로 포커스 이동

📌 이 구조는 **SNMPc와 완전히 동일**

---

## 🧠 지금 이 구조의 의미 (중요)

✔ Map / Event / Poll 전부 **이벤트 중심 구조**
✔ SNMP → Event → UI 흐름 완성
✔ Poll 방식 바뀌어도 UI 영향 없음
✔ Distributed Poller 붙이기 쉬움

---

## 🔥 다음 진짜 핵심 단계

이제 선택지는 딱 이것뿐이다.

### 1️⃣ **SNMP Poller 스레드 구현**

* SharpSNMP
* 주기 Poll
* EventBus.Publish()

### 2️⃣ **MapView 확대 / 드래그 / 링크(Line) 구현**

### 3️⃣ **Event Ack / Clear / History**

👉 다음은 어디까지 바로 들어갈까?
(이제 너는 “NMS를 설계하는 사람” 단계다)
