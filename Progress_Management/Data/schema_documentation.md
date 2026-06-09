# スキーマ定義書

このドキュメントは `schema.sql` に基づき、テーブル定義と補足をまとめたものです。

## テーブル一覧

### `scenarios`
- 説明: 表示シナリオ（レビュー用の表示切替単位）
- 主キー: `id`
- カラム:
  - `id` TEXT PRIMARY KEY
  - `name` TEXT NOT NULL
  - `purpose` TEXT NOT NULL
  - `due_status` TEXT NOT NULL
  - `proposal_status` TEXT NOT NULL
  - `sort_order` INTEGER NOT NULL DEFAULT 0

### `workers`
- 説明: 担当者マスター（現状、途中担当変更は扱わない）
- 主キー: `id`
- カラム:
  - `id` TEXT PRIMARY KEY
  - `name` TEXT NOT NULL
  - `sort_order` INTEGER NOT NULL DEFAULT 0

### `work_tasks`
- 説明: ガントチャートの主データ（予定・実績・提案・作業量・難易度を集約）
- 主キー: `id`
- 外部キー:
  - `scenario_id` -> `scenarios(id)`
  - `worker_id` -> `workers(id)`
- カラム:
  - `id` TEXT PRIMARY KEY
  - `scenario_id` TEXT NOT NULL
  - `name` TEXT NOT NULL
  - `worker_id` TEXT NOT NULL
  - `difficulty` INTEGER NOT NULL
  - `planned_workload` REAL NOT NULL
  - `actual_workload` REAL NOT NULL
  - `output_unit` TEXT NOT NULL
  - `baseline_start` TEXT NOT NULL
  - `baseline_end` TEXT NOT NULL
  - `revised_start` TEXT NULL
  - `revised_end` TEXT NULL
  - `actual_start` TEXT NULL
  - `actual_end` TEXT NULL
  - `proposal_start` TEXT NULL
  - `proposal_end` TEXT NULL
  - `status` TEXT NOT NULL
  - `note` TEXT NOT NULL DEFAULT ''
  - `sort_order` INTEGER NOT NULL DEFAULT 0

### `task_dependencies`
- 説明: 作業間の先行後続関係（依存線表示用）
- 主キー: `(scenario_id, task_id, depends_on_task_id)`
- 外部キー:
  - `scenario_id` -> `scenarios(id)`
  - `task_id` -> `work_tasks(id)`
  - `depends_on_task_id` -> `work_tasks(id)`
- カラム:
  - `scenario_id` TEXT NOT NULL
  - `task_id` TEXT NOT NULL
  - `depends_on_task_id` TEXT NOT NULL
  - `sort_order` INTEGER NOT NULL DEFAULT 0

## インデックス
- `idx_work_tasks_scenario_order` ON `work_tasks(scenario_id, sort_order, id)`
- `idx_task_dependencies_task` ON `task_dependencies(scenario_id, task_id, sort_order)`

## 備考
- 日付／時刻系カラムはすべて `TEXT` 型で定義されているため、アプリ側でフォーマットと比較ルールを統一する必要があります。
- `work_tasks` の `worker_id` と `scenario_id` は必須で、タスクは常にシナリオと担当者に紐づきます。
