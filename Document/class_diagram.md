# クラス図定義書

データベース定義に基づいた、ガントチャート管理ソリューションのクラス図です。

```mermaid
classDiagram
    class Scenario {
        +String id
        +String name
        +String purpose
        +String due_status
        +String proposal_status
        +int sort_order
    }

    class Worker {
        +String id
        +String name
        +int sort_order
    }

    class WorkTask {
        +String id
        +String scenario_id
        +String name
        +String worker_id
        +int difficulty
        +double planned_workload
        +double actual_workload
        +String output_unit
        +String baseline_start
        +String baseline_end
        +String revised_start
        +String revised_end
        +String? actual_start
        +String? actual_end
        +String? proposal_start
        +String? proposal_end
        +String status
        +String note
        +int sort_order
    }

    class TaskDependency {
        +String scenario_id
        +String task_id
        +String depends_on_task_id
        +int sort_order
    }

    %% Relationships
    Scenario "1" *-- "*" WorkTask : contains
    Worker "1" -- "*" WorkTask : assigns
    Scenario "1" *-- "*" TaskDependency : defines_flow
    WorkTask "1" -- "*" TaskDependency : has_successors (task_id)
    WorkTask "1" -- "*" TaskDependency : has_predecessors (depends_on_task_id)
```

## 補足

- `WorkTask` クラスの各日付フィールドは、データベース上は `TEXT` ですが、アプリケーション層では `DateTime` もしくは `String` (ISO 8601形式) として扱われることを想定しています。
- `TaskDependency` は、特定の `Scenario` 内における `WorkTask` 間の自己参照多対多関係を解決するための連関エンティティです。
