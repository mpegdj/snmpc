좋아 👍
그럼 **다음 단계 = 실제로 화면이 “살아 움직이게” 만드는 단계**로 간다.

👉 **Selection Tool 클릭 → 중앙 MDI(View) 열기 (MVVM 연결)**
이게 되면 이후 Map / MIB / Trend / Event 전부 같은 패턴으로 확장 가능하다.

---

# 🔥 2단계 구현

## Selection Tool → View 생성 (MVVM 패턴)

---

## 1️⃣ 목표 동작 (명확히)

1. 좌측 **Selection Tool(TreeView)** 에서 항목 클릭
2. 중앙 **MDI(TabControl)** 에 View가 열린다
3. 이미 열린 View면 **중복 생성 ❌, 해당 탭으로 포커스 이동**

👉 이 동작이 SNMPc 콘솔의 핵심 UX다.

---

## 2️⃣ View 모델 공통 구조

### 📌 View를 표현하는 공통 클래스

```csharp
public class ViewItem
{
    public string Title { get; set; }
    public object View { get; set; }
}
```

---

## 3️⃣ MainWindowViewModel

📌 **MDI 탭 관리의 중심**

```csharp
using System.Collections.ObjectModel;
using System.Linq;

public class MainWindowViewModel
{
    public ObservableCollection<ViewItem> OpenViews { get; }
        = new ObservableCollection<ViewItem>();

    public void OpenView(string title, object view)
    {
        var existing = OpenViews.FirstOrDefault(v => v.Title == title);
        if (existing != null)
            return; // 이미 열려 있으면 재사용

        OpenViews.Add(new ViewItem
        {
            Title = title,
            View = view
        });
    }
}
```

---

## 4️⃣ MainWindow.xaml.cs

📌 ViewModel 연결

```csharp
public partial class MainWindow : Window
{
    public MainWindowViewModel ViewModel { get; }

    public MainWindow()
    {
        InitializeComponent();
        ViewModel = new MainWindowViewModel();
        DataContext = ViewModel;

        // 기본 View 하나 열어두기
        ViewModel.OpenView("Root Map", new MapView());
    }
}
```

---

## 5️⃣ Selection Tool → 이벤트 전달 구조

### 📌 SelectionToolViewModel

```csharp
public class SelectionItem
{
    public string Name { get; set; }
    public string Type { get; set; } // Map, MIB, Trend, Event
}
```

```csharp
using System.Collections.ObjectModel;

public class SelectionToolViewModel
{
    public ObservableCollection<SelectionItem> MapItems { get; }
        = new ObservableCollection<SelectionItem>
        {
            new SelectionItem { Name = "Root Map", Type = "Map" },
            new SelectionItem { Name = "DataCenter Map", Type = "Map" }
        };
}
```

---

## 6️⃣ SelectionTool.xaml (바인딩 + 클릭 이벤트)

```xml
<TreeView ItemsSource="{Binding MapItems}"
          SelectedItemChanged="OnItemSelected">
    <TreeView.ItemTemplate>
        <DataTemplate>
            <TextBlock Text="{Binding Name}"/>
        </DataTemplate>
    </TreeView.ItemTemplate>
</TreeView>
```

---

## 7️⃣ SelectionTool.xaml.cs

📌 **MainWindow ViewModel 호출**

```csharp
public partial class SelectionTool : UserControl
{
    public SelectionTool()
    {
        InitializeComponent();
        DataContext = new SelectionToolViewModel();
    }

    private void OnItemSelected(object sender, RoutedPropertyChangedEventArgs<object> e)
    {
        if (e.NewValue is SelectionItem item)
        {
            var mainVm = ((MainWindow)Application.Current.MainWindow).ViewModel;

            switch (item.Type)
            {
                case "Map":
                    mainVm.OpenView(item.Name, new MapView());
                    break;

                case "MIB":
                    mainVm.OpenView(item.Name, new MibTableView());
                    break;

                case "Trend":
                    mainVm.OpenView(item.Name, new TrendView());
                    break;
            }
        }
    }
}
```

📌 **SNMPc와 동일한 “선택 → View 생성” 패턴**

---

## 8️⃣ 지금 상태에서 가능한 것

✔ Tree 클릭 → 탭 생성
✔ 같은 항목 다시 클릭 → 중복 탭 안 생김
✔ View 타입 확장 매우 쉬움
✔ Map / MIB / Trend / Event 모두 동일 구조

---

## 9️⃣ SNMPc 기준 다음 핵심 구현 순서

이제 진짜 “NMS”가 된다.
다음 중 하나를 바로 들어가자.

### 🔥 추천 1순위

**MapView 고도화**

* Device 객체 모델
* 상태 색상 (Up/Down)
* 링크(Line) 표현
* Zoom / Drag

### 🔥 추천 2순위

**Event → Map 연동**

* Event 발생
* 장비 상태 변경
* Map 색상 즉시 반영

### 🔥 추천 3순위

**SNMP Poll 결과 → View 바인딩 구조**

---

👉 다음은 뭐로 갈까?
**1️⃣ MapView 실제 NMS답게 만들기**
**2️⃣ Event 처리 파이프라인 설계**

(이제부터는 “제품 만드는 단계”다)
