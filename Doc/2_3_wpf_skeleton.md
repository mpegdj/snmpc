좋아. 이제 **말로 설명 끝**이고
👉 **바로 구현 가능한 수준의 WPF XAML 스켈레톤**을 **SNMPc 콘솔 구조 그대로** 제공할게.

아래는 **“실행되며 점진적으로 살을 붙일 수 있는 뼈대”**다.
(디자인 ❌ / 기능 ❌ / 구조 ✅)

---

# 🖥 SNMPc 스타일 NMS

## 화면별 XAML 스켈레톤 (WPF)

---

## 0️⃣ 프로젝트 기본 구조 (권장)

```
NmsClient
 ├─ App.xaml
 ├─ MainWindow.xaml          ← 콘솔 Shell
 ├─ Views
 │   ├─ MapView.xaml
 │   ├─ EventLogView.xaml
 │   ├─ MibTableView.xaml
 │   ├─ MibGraphView.xaml
 │   └─ TrendView.xaml
 ├─ Controls
 │   ├─ SelectionTool.xaml
 │   └─ ToolBars.xaml
 └─ ViewModels
```

---

## 1️⃣ MainWindow.xaml (콘솔 Shell)

📌 **SNMPc 전체 레이아웃의 핵심**

```xml
<Window x:Class="NmsClient.MainWindow"
        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
        Title="SNMP NMS Console" Height="900" Width="1400">

    <DockPanel>

        <!-- Menu Bar -->
        <Menu DockPanel.Dock="Top">
            <MenuItem Header="_File"/>
            <MenuItem Header="_Edit"/>
            <MenuItem Header="_View"/>
            <MenuItem Header="_Tools"/>
            <MenuItem Header="_Help"/>
        </Menu>

        <!-- ToolBars -->
        <StackPanel DockPanel.Dock="Top">
            <ToolBarTray>
                <ToolBar>
                    <Button Content="Open Map"/>
                    <Button Content="Poll Now"/>
                    <Button Content="MIB"/>
                </ToolBar>

                <ToolBar>
                    <Button Content="Add Device"/>
                    <Button Content="Add Link"/>
                </ToolBar>
            </ToolBarTray>
        </StackPanel>

        <!-- Bottom Event Log -->
        <Border DockPanel.Dock="Bottom" Height="200">
            <ContentControl Content="{Binding EventLogView}"/>
        </Border>

        <!-- Main Area -->
        <Grid>
            <Grid.ColumnDefinitions>
                <ColumnDefinition Width="250"/>
                <ColumnDefinition Width="*"/>
            </Grid.ColumnDefinitions>

            <!-- Selection Tool -->
            <ContentControl Grid.Column="0"
                            Content="{Binding SelectionToolView}"/>

            <!-- MDI Area -->
            <TabControl Grid.Column="1"
                        ItemsSource="{Binding OpenViews}">
                <TabControl.ItemTemplate>
                    <DataTemplate>
                        <TextBlock Text="{Binding Title}"/>
                    </DataTemplate>
                </TabControl.ItemTemplate>
                <TabControl.ContentTemplate>
                    <DataTemplate>
                        <ContentControl Content="{Binding View}"/>
                    </DataTemplate>
                </TabControl.ContentTemplate>
            </TabControl>

        </Grid>

    </DockPanel>
</Window>
```

✔ SNMPc 구조 그대로
✔ MDI = `TabControl`
✔ Event Log 하단 고정

---

## 2️⃣ SelectionTool.xaml (좌측 패널)

```xml
<UserControl x:Class="NmsClient.Controls.SelectionTool"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <TabControl>

        <!-- Map Tab -->
        <TabItem Header="Map">
            <TreeView>
                <TreeViewItem Header="Root Map">
                    <TreeViewItem Header="Subnet"/>
                    <TreeViewItem Header="Device"/>
                </TreeViewItem>
            </TreeView>
        </TabItem>

        <!-- MIB Tab -->
        <TabItem Header="MIB">
            <TreeView>
                <TreeViewItem Header="Standard MIBs"/>
                <TreeViewItem Header="Vendor MIBs"/>
            </TreeView>
        </TabItem>

        <!-- Trend Tab -->
        <TabItem Header="Trend">
            <TreeView>
                <TreeViewItem Header="Interface Utilization"/>
                <TreeViewItem Header="CPU Load"/>
            </TreeView>
        </TabItem>

        <!-- Event Tab -->
        <TabItem Header="Event">
            <TreeView>
                <TreeViewItem Header="All Events"/>
                <TreeViewItem Header="Critical"/>
            </TreeView>
        </TabItem>

        <!-- Menu Tab -->
        <TabItem Header="Menu">
            <ListBox>
                <ListBoxItem Content="Custom Tool 1"/>
            </ListBox>
        </TabItem>

    </TabControl>
</UserControl>
```

📌 **모든 View는 여기서 시작**

---

## 3️⃣ MapView.xaml (토폴로지 화면)

```xml
<UserControl x:Class="NmsClient.Views.MapView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Canvas Background="LightGray">

        <!-- Device Icon -->
        <Ellipse Width="40" Height="40"
                 Fill="Green"
                 Canvas.Left="100"
                 Canvas.Top="100"/>

        <TextBlock Text="Device-1"
                   Canvas.Left="95"
                   Canvas.Top="145"/>

    </Canvas>
</UserControl>
```

✔ 나중에 **Zoom / Drag / Link** 붙이기 쉬움
✔ 실제 SNMPc Map 구조와 동일

---

## 4️⃣ EventLogView.xaml (하단 고정)

```xml
<UserControl x:Class="NmsClient.Views.EventLogView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <DataGrid AutoGenerateColumns="False" IsReadOnly="True">
        <DataGrid.Columns>
            <DataGridTextColumn Header="Time" Binding="{Binding Time}"/>
            <DataGridTextColumn Header="Severity" Binding="{Binding Severity}"/>
            <DataGridTextColumn Header="Source" Binding="{Binding Source}"/>
            <DataGridTextColumn Header="Message" Binding="{Binding Message}"/>
        </DataGrid.Columns>
    </DataGrid>
</UserControl>
```

📌 SNMPc처럼 **항상 표시**

---

## 5️⃣ MibTableView.xaml

```xml
<UserControl x:Class="NmsClient.Views.MibTableView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <DataGrid AutoGenerateColumns="True"/>
</UserControl>
```

✔ SNMP Get/Walk 결과용

---

## 6️⃣ MibGraphView.xaml

```xml
<UserControl x:Class="NmsClient.Views.MibGraphView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Grid>
        <TextBlock Text="MIB Graph View"
                   HorizontalAlignment="Center"
                   VerticalAlignment="Center"/>
    </Grid>
</UserControl>
```

📌 실제 그래프는 나중에 LiveCharts 등으로 교체

---

## 7️⃣ TrendView.xaml

```xml
<UserControl x:Class="NmsClient.Views.TrendView"
             xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
             xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">

    <Grid>
        <TextBlock Text="Trend Report View"
                   FontSize="20"
                   HorizontalAlignment="Center"
                   VerticalAlignment="Center"/>
    </Grid>
</UserControl>
```

---

## 🔥 여기까지 하면 얻는 것

✔ SNMPc 콘솔과 **구조 100% 동일**
✔ 실행 즉시 **MDI / Map / Event / Selection Tool 작동**
✔ 이제는 “기획”이 아니라 **구현 단계**

---

## 다음 단계 (추천 순서)

1️⃣ **SelectionTool ↔ View 열기 바인딩**
2️⃣ **MapView에서 Device 객체 모델링**
3️⃣ **Event → Map 상태 반영 로직**
4️⃣ **SNMP Poll 결과를 View에 연결**

👉 다음은
**“Selection Tool 클릭 → View 열리는 MVVM 연결 코드”**로 갈까,
아니면 **“MapView 확대/드래그/링크 구조 설계”**부터 갈까?
