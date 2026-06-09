PRAGMA foreign_keys = ON;

-- 表示シナリオ。レビュー用の「メインの流れ」「オンスケジュール」等を切り替える単位。
CREATE TABLE IF NOT EXISTS scenarios (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    purpose TEXT NOT NULL,
    due_status TEXT NOT NULL,
    proposal_status TEXT NOT NULL,
    sort_order INTEGER NOT NULL DEFAULT 0
);

-- 担当者マスター。現時点では作業の途中担当変更は扱わない。
CREATE TABLE IF NOT EXISTS workers (
    id TEXT PRIMARY KEY,
    name TEXT NOT NULL,
    sort_order INTEGER NOT NULL DEFAULT 0
);

-- ガントチャートの主データ。予定、実績、再スケ提案、作業量、難易度をここに集約する。
CREATE TABLE IF NOT EXISTS work_tasks (
    id TEXT PRIMARY KEY,
    scenario_id TEXT NOT NULL,
    name TEXT NOT NULL,
    worker_id TEXT NOT NULL,
    difficulty INTEGER NOT NULL,
    planned_workload REAL NOT NULL,
    actual_workload REAL NOT NULL,
    output_unit TEXT NOT NULL,
    baseline_start TEXT NOT NULL,
    baseline_end TEXT NOT NULL,
    revised_start TEXT NULL,
    revised_end TEXT NULL,
    actual_start TEXT NULL,
    actual_end TEXT NULL,
    proposal_start TEXT NULL,
    proposal_end TEXT NULL,
    status TEXT NOT NULL,
    note TEXT NOT NULL DEFAULT '',

    sort_order INTEGER NOT NULL DEFAULT 0,
    FOREIGN KEY (scenario_id) REFERENCES scenarios(id),
    FOREIGN KEY (worker_id) REFERENCES workers(id)
);

-- 作業間の先行後続関係。プロジェクト進捗表示の依存線に使う。
CREATE TABLE IF NOT EXISTS task_dependencies (
    scenario_id TEXT NOT NULL,
    task_id TEXT NOT NULL,
    depends_on_task_id TEXT NOT NULL,
    sort_order INTEGER NOT NULL DEFAULT 0,
    PRIMARY KEY (scenario_id, task_id, depends_on_task_id),
    FOREIGN KEY (scenario_id) REFERENCES scenarios(id),
    FOREIGN KEY (task_id) REFERENCES work_tasks(id),
    FOREIGN KEY (depends_on_task_id) REFERENCES work_tasks(id)
);

CREATE INDEX IF NOT EXISTS idx_work_tasks_scenario_order
    ON work_tasks(scenario_id, sort_order, id);

CREATE INDEX IF NOT EXISTS idx_task_dependencies_task
    ON task_dependencies(scenario_id, task_id, sort_order);
