# Progress Management

WinUI 3 / C# による進捗ガント査証プロトです。

## 構成

- `Progress_Management/`: WinUI 3 アプリ本体
- `Progress_Management/Data/schema.sql`: SQLite3 スキーマ
- `Progress_Management/Data/seed.sql`: JSON固定データから変換した初期SQLデータ
- `Progress_Management/Models/ProgressData.cs`: 管理データモデル
- `Progress_Management/Services/ScenarioRepository.cs`: SQLite3読み込み
- `Prototype/`: HTML/JavaScript版の査証プロト

## 現時点の実装範囲

- シナリオ切替
- 個人チャート / プロジェクトチャート切替
- 日 / 週 / 月切替
- 当初予定、リスケ後予定、実績、再スケ提案の表示
- 先行後続関係の簡易表示
- 遅延/逆転マーカー
- ガント要素クリック時の詳細表示

## SQLite3

アプリは初回起動時に以下へSQLite3 DBを作成します。

```text
%LOCALAPPDATA%\Progress_Management\progress_management.db
```

DBeaverでマスターや検証データを編集する場合は、このDBを開きます。

初期データを作り直したい場合は、上記DBを削除してアプリを起動してください。`Data/schema.sql` と `Data/seed.sql` から再作成されます。

## 注意

このPCでは `dotnet` がPATH上に見つかりませんでしたが、`C:\Program Files\dotnet\dotnet.exe` を直接指定してビルド確認済みです。
