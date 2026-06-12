# データベース定義書

## 1\. 概要

本データベースは、プロジェクト管理におけるガントチャートの進捗管理、および表示シナリオごとのタスク関係を保持するために設計されています。

\---

## 2\. テーブル定義

### 2.1 scenarios (表示シナリオ)

レビュー単位や進捗状況の切り替え用シナリオを管理します。

|カラム名|データ型|制約|説明|
|-|-|-|-|
|`id`|TEXT|PRIMARY KEY|シナリオID|
|`name`|TEXT|NOT NULL|シナリオ名|
|`purpose`|TEXT|NOT NULL|目的|
|`due\\\_status`|TEXT|NOT NULL|期限ステータス|
|`proposal\\\_status`|TEXT|NOT NULL|提案ステータス|
|`sort\\\_order`|INTEGER|NOT NULL|表示順序|

### 2.2 workers (担当者マスター)

作業を担当するユーザー情報を管理します。

|カラム名|データ型|制約|説明|
|-|-|-|-|
|`id`|TEXT|PRIMARY KEY|担当者ID|
|`name`|TEXT|NOT NULL|担当者名|
|`sort\\\_order`|INTEGER|NOT NULL|表示順序|

### 2.3 work\_tasks (ガントチャート主データ)

タスクの詳細情報、予定、実績、提案状況を集約します。

|カラム名|データ型|制約|説明|
|-|-|-|-|
|`id`|TEXT|PRIMARY KEY|タスクID|
|`scenario\\\_id`|TEXT|FK, NOT NULL|シナリオID|
|`name`|TEXT|NOT NULL|タスク名|
|`worker\\\_id`|TEXT|FK, NOT NULL|担当者ID|
|`difficulty`|INTEGER|NOT NULL|難易度|
|`planned\\\_workload`|REAL|NOT NULL|予定作業量|
|`actual\\\_workload`|REAL|NOT NULL|実績作業量|
|`output\\\_unit`|TEXT|NOT NULL|出力単位|
|`baseline\\\_start`|TEXT|NOT NULL|ベースライン開始日|
|`baseline\\\_end`|TEXT|NOT NULL|ベースライン終了日|
|`revised\\\_start`|TEXT|NOT NULL|修正計画開始日|
|`revised\\\_end`|TEXT|NOT NULL|修正計画終了日|
|`actual\\\_start`|TEXT|NULL|実績開始日|
|`actual\\\_end`|TEXT|NULL|実績終了日|
|`proposal\\\_start`|TEXT|NULL|提案開始日|
|`proposal\\\_end`|TEXT|NULL|提案終了日|
|`status`|TEXT|NOT NULL|進捗ステータス|
|`note`|TEXT|NOT NULL|備考|
|`sort\\\_order`|INTEGER|NOT NULL|表示順序|

### 2.4 task\_dependencies (タスク依存関係)

タスク間の先行・後続関係を保持します。

|カラム名|データ型|制約|説明|
|-|-|-|-|
|`scenario\\\_id`|TEXT|FK, PK, NOT NULL|シナリオID|
|`task\\\_id`|TEXT|FK, PK, NOT NULL|対象タスクID|
|`depends\\\_on\\\_task\\\_id`|TEXT|FK, PK, NOT NULL|先行タスクID|
|`sort\\\_order`|INTEGER|NOT NULL|表示順序|

\---

## 3\. インデックス定義

* **`idx\\\_work\\\_tasks\\\_scenario\\\_order`**

  * 対象テーブル: `work\\\_tasks`
  * カラム: `(scenario\\\_id, sort\\\_order, id)`
* **`idx\\\_task\\\_dependencies\\\_task`**

  * 対象テーブル: `task\\\_dependencies`
  * カラム: `(scenario\\\_id, task\\\_id, sort\\\_order)`

