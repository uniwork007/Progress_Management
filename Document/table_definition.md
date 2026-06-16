# データベース定義書

## 1\. 概要

本データベースは、プロジェクト管理におけるガントチャートの進捗管理、および表示シナリオごとのタスク関係を保持するために設計されています。

\---

## 2\. テーブル定義

### 2.1 scenarios (表示シナリオ)

レビュー単位や進捗状況の切り替え用シナリオを管理します。

| カラム名          | データ型 | 制約        | 説明           |
| ----------------- | -------- | ----------- | -------------- |
| `id`              | TEXT     | PRIMARY KEY | シナリオID     |
| `name`            | TEXT     | NOT NULL    | シナリオ名     |
| `purpose`         | TEXT     | NOT NULL    | 目的           |
| `due_status`      | TEXT     | NOT NULL    | 期限ステータス |
| `proposal_status` | TEXT     | NOT NULL    | 提案ステータス |
| `sort_order`      | INTEGER  | NOT NULL    | 表示順序       |

### 2.2 workers (担当者マスター)

作業を担当するユーザー情報を管理します。

| カラム名     | データ型 | 制約        | 説明     |
| ------------ | -------- | ----------- | -------- |
| `id`         | TEXT     | PRIMARY KEY | 担当者ID |
| `name`       | TEXT     | NOT NULL    | 担当者名 |
| `sort_order` | INTEGER  | NOT NULL    | 表示順序 |

### 2.3 work_tasks (ガントチャート主データ)

タスクの詳細情報、予定、実績、提案状況を集約します。

| カラム名          | データ型 | 制約         | 説明               |
| ----------------- | -------- | ------------ | ------------------ |
| `id`              | TEXT     | PRIMARY KEY  | タスクID           |
| `scenarioid`      | TEXT     | FK, NOT NULL | シナリオID         |
| `name`            | TEXT     | NOT NULL     | タスク名           |
| `workerid`        | TEXT     | FK, NOT NULL | 担当者ID           |
| `difficulty`      | INTEGER  | NOT NULL     | 難易度             |
| `plannedworkload` | REAL     | NOT NULL     | 予定作業量         |
| `actualworkload`  | REAL     | NOT NULL     | 実績作業量         |
| `outputunit`      | TEXT     | NOT NULL     | 出力単位           |
| `baselinestart`   | TEXT     | NOT NULL     | ベースライン開始日 |
| `baselineend`     | TEXT     | NOT NULL     | ベースライン終了日 |
| `revisedstart`    | TEXT     | NOT NULL     | 修正計画開始日     |
| `revisedend`      | TEXT     | NOT NULL     | 修正計画終了日     |
| `actualstart`     | TEXT     | NULL         | 実績開始日         |
| `actualend`       | TEXT     | NULL         | 実績終了日         |
| `proposalstart`   | TEXT     | NULL         | 提案開始日         |
| `proposalend`     | TEXT     | NULL         | 提案終了日         |
| `status`          | TEXT     | NOT NULL     | 進捗ステータス     |
| `note`            | TEXT     | NOT NULL     | 備考               |
| `sortorder`       | INTEGER  | NOT NULL     | 表示順序           |

### 2.4 task_dependencies (タスク依存関係)

タスク間の先行・後続関係を保持します。

| カラム名          | データ型 | 制約             | 説明         |
| ----------------- | -------- | ---------------- | ------------ |
| `scenarioid`      | TEXT     | FK, PK, NOT NULL | シナリオID   |
| `taskid`          | TEXT     | FK, PK, NOT NULL | 対象タスクID |
| `dependsontaskid` | TEXT     | FK, PK, NOT NULL | 先行タスクID |
| `sortorder`       | INTEGER  | NOT NULL         | 表示順序     |

\---

## 3\. インデックス定義

- **`idxworktasksscenarioorder`**
    - 対象テーブル: `worktasks`
    - カラム: `(scenarioid, sortorder, id)`

- **`idxtaskdependenciestask`**
    - 対象テーブル: `taskdependencies`
    - カラム: `(scenarioid, taskid, sortorder)`
