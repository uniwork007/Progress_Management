# 画面遷移図

**画面一覧**

- **Main:** [Progress_Management/MainWindow.xaml](Progress_Management/MainWindow.xaml)
- **App（起動／共通資源）:** [Progress_Management/App.xaml](Progress_Management/App.xaml)
- **作業編集ウィンドウ:** [Progress_Management/WorkTaskEditorWindow.xaml](Progress_Management/WorkTaskEditorWindow.xaml)
- **作業者メンテナンス（ダイアログ）:** [Progress_Management/WorkerMaintenanceDialog.xaml](Progress_Management/WorkerMaintenanceDialog.xaml)

**Mermaid 画面遷移**

```mermaid
flowchart LR
  MW["MainWindow (メイン画面)"]
  WME["WorkTaskEditorWindow (作業編集)"]
  WMD["WorkerMaintenanceDialog (担当者メンテ)"]

  MW -- "作業ID入力 + 開くボタン" --> WME
  MW -- "グラフオブジェクトをクリック" --> WME
  MW -- "新規ボタン" --> WME
  MW -- "担当者ボタン" --> WMD

  WME & WMD -- "閉じる/保存" --> MW

  click WME "Progress_Management/WorkTaskEditorWindow.xaml"
  click WMD "Progress_Management/WorkerMaintenanceDialog.xaml"
  click MW "Progress_Management/MainWindow.xaml"
```
