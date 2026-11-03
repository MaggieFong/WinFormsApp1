這是一個靈巧手專案，APP 會包含以下功能
1. 靈巧手手指有觸摸動作，APP UI上該指節會顯示紅圈
2. 設有多種手勢按鈕，執行後靈巧手就會做出該手勢

## 介面

企業藍白風格（主色 `#003A91`）：白色頁首＋藍色分隔線、淺灰底、白色卡片。

| 區塊 | 說明 |
|---|---|
| 手部狀態 HAND STATUS | 手部影像，觸碰中的指節顯示紅圈（含呼吸動畫），指尖上方顯示彎曲度 %。沒接硬體時可點擊指節模擬觸碰。 |
| 手指彎曲度 FINGER FLEXION | 五指滑桿，支援拖曳、滾輪、方向鍵（Shift = 10%）。 |
| 預設手勢 GESTURE PRESETS | 張開、握拳、比讚、剪刀、OK、指向，套用時有平滑過渡動畫。 |
| 事件紀錄 EVENT LOG | 手勢與觸碰事件的時間紀錄。 |

## 程式結構

```
WinFormsApp1/
├─ Assets/hand.png      手部影像（內嵌資源）
├─ UI/Theme.cs          配色與字型，換色只改這裡
├─ UI/Controls.cs       卡片、扁平滑桿、扁平按鈕、頁首、狀態列
├─ UI/HandView.cs       手部視圖與觸碰紅圈
└─ Form1.cs             主畫面版面與邏輯
```

## 串接硬體

- **送出手勢**：`Form1.cs` 的 `SendToDevice(int[] targets)`，五指目標值 0~100（拇、食、中、無名、小）。
- **觸碰回報**：呼叫 `FormHandDemo.SetTouch(finger, segment, touched)`，可從背景執行緒呼叫。
  - `finger`：0 拇指、1 食指、2 中指、3 無名指、4 小指
  - `segment`：0 近端 … 最後一節為指尖（拇指 2 節，其餘 3 節）
- 接上硬體後可將 `HandView.AllowClickToSimulate` 設為 `false`，並呼叫 `_header.SetConnection(true, "Connected")`。
- 紅圈位置定義在 `UI/HandView.cs` 的 `SegmentPoints`（以影像座標正規化），更換手部影像時需一併調整。
