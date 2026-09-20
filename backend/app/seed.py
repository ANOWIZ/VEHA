"""Синтетические данные для локального прототипа. Не сведения о реальных компаниях.

Организации, люди, реквизиты, цены и финансовые показатели вымышлены.
Идемпотентно: повторный запуск пропускается.

Запуск:  docker compose -p psa run --rm backend uv run python -m app.seed
Наполняет: пользователи с ролями и ставками, клиенты, каталог (вендоры/продукты/
прайсы), проект со стадиями и участниками, задачи, вехи, трудозатраты
(утверждённые + ожидающие), расчёт ТКП, бюджет и факт. Прогоняет реальную
бизнес-логику сервисов (генерация кода проекта, переходы стадий, снимок
себестоимости при утверждении, пересчёт маржи расчёта).
"""

from __future__ import annotations

import asyncio
from datetime import UTC, date, datetime
from decimal import Decimal

from app.db.session import SessionFactory
from app.models.enums import (
    STAGE_ORDER,
    CostCategory,
    LicensingModel,
    ProjectMemberRole,
    ProjectStatus,
    ProjectType,
    QuoteLineKind,
    QuoteStatus,
    RiskCategory,
    RiskResponse,
    RiskStatus,
    Stage,
)
from app.repositories.user_repo import UserRepository
from app.schemas.catalog import PriceItemCreate, ProductCreate, VendorCreate
from app.schemas.client import ClientCreate
from app.schemas.customer import ActionItemCreate
from app.schemas.finance import ActualCostCreate, BudgetUpsert
from app.schemas.project import (
    MemberCreate,
    MilestoneCreate,
    ProjectCreate,
    ProjectUpdate,
)
from app.schemas.quote import QuoteCreate, QuoteLineInput
from app.schemas.resource import ResourcePlanUpsert
from app.schemas.risk import RiskCreate, RiskUpdate
from app.schemas.task import TaskCreate
from app.schemas.timesheet import TimeEntryCreate
from app.services.catalog_service import CatalogService
from app.services.client_service import ClientService
from app.services.customer_service import CustomerService
from app.services.finance_service import FinanceService
from app.services.project_service import ProjectService
from app.services.quote_service import QuoteService
from app.services.resource_service import ResourceService
from app.services.risk_service import RiskService
from app.services.task_service import TaskService
from app.services.timesheet_service import TimesheetService
from app.services.user_service import UserService

USERS = [
    ("admin.demo", "Демо Пользователь 01", ["admin"], None),
    ("director.demo", "Демо Пользователь 02", ["director"], None),
    ("pm.demo", "Демо Пользователь 03", ["pm"], "1800"),
    ("pm2.demo", "Демо Пользователь 04", ["pm"], "1700"),
    ("presale.demo", "Демо Пользователь 05", ["presale"], None),
    ("finance.demo", "Демо Пользователь 06", ["finance"], None),
    ("eng.demo", "Демо Пользователь 07", ["engineer"], "1300"),
    ("eng2.demo", "Демо Пользователь 08", ["engineer"], "1100"),
    ("eng3.demo", "Демо Пользователь 09", ["engineer"], "1400"),
    ("eng4.demo", "Демо Пользователь 10", ["engineer"], "1100"),
]


async def _advance(proj_svc, project, target: Stage, actor_id):
    """Последовательно перевести проект на целевую стадию (через все промежуточные)."""
    while project.stage != target:
        nxt = STAGE_ORDER[STAGE_ORDER.index(project.stage) + 1]
        project = await proj_svc.change_stage(project.id, nxt, None, actor_id)
    return project


async def main() -> None:
    async with SessionFactory() as session:
        users_repo = UserRepository(session)
        if await users_repo.get_by_username("admin.demo") is not None:
            print("Демо-данные уже есть — пропускаю.")
            return

        user_svc = UserService(session)
        users: dict[str, object] = {}
        for username, full_name, roles, rate in USERS:
            u = await users_repo.create(
                keycloak_id=f"dev:{username}",
                username=username,
                email=f"{username}@team.example.com",
                full_name=full_name,
                department="Департамент внедрения",
                position=full_name.split()[1] if " " in full_name else "",
                roles=roles,
                is_active=True,
            )
            users[username] = u
            if rate:
                await user_svc.set_cost_rate(u.id, Decimal(rate), date(2025, 1, 1))
        admin, pm, eng, eng2 = (
            users["admin.demo"],
            users["pm.demo"],
            users["eng.demo"],
            users["eng2.demo"],
        )

        # --- Клиенты ---
        client_svc = ClientService(session)
        client01 = await client_svc.create(
            ClientCreate(
                name="Демо-клиент 01",
                inn="0000000001",
                industry="Энергетика",
                is_kii=True,
            )
        )
        client02 = await client_svc.create(
            ClientCreate(name="Демо-клиент 02", inn="0000000002", industry="Розница")
        )
        client03 = await client_svc.create(
            ClientCreate(
                name="Демо-клиент 03", inn="0000000003", industry="Финансы", is_kii=True
            )
        )
        client04 = await client_svc.create(
            ClientCreate(name="Демо-клиент 04", inn="0000000004", industry="Логистика")
        )

        # Контакты Заказчиков (портал).
        client_user = await users_repo.create(
            keycloak_id="dev:client.demo",
            username="client.demo",
            email="client.demo@client01.example.com",
            full_name="Демо Пользователь 11",
            department="Демо-клиент 01",
            position="Руководитель ИТ",
            roles=["client"],
            is_active=True,
            client_id=client01.id,
        )
        client2_user = await users_repo.create(
            keycloak_id="dev:client2.demo",
            username="client2.demo",
            email="client2.demo@client02.example.com",
            full_name="Демо Пользователь 12",
            department="Демо-клиент 02",
            position="ИТ-директор",
            roles=["client"],
            is_active=True,
            client_id=client02.id,
        )

        # --- Каталог ---
        cat = CatalogService(session)
        demo_vendor_01 = await cat.create_vendor(VendorCreate(name="Демо-вендор 01", country="Россия"))
        demo_vendor_02 = await cat.create_vendor(
            VendorCreate(name="Демо-вендор 02", country="Россия")
        )
        demo_vendor_03 = await cat.create_vendor(VendorCreate(name="Демо-вендор 03", country="США"))

        demo_os_01 = await cat.create_product(
            ProductCreate(
                vendor_id=demo_vendor_01.id,
                name="Демо-ОС 01",
                edition="Сервер",
                licensing_model=LicensingModel.PER_SERVER,
                price_currency="RUB",
                is_russian_registry=True,
            )
        )
        await cat.add_price(
            demo_os_01.id,
            PriceItemCreate(metric="сервер", price=Decimal("42000"), valid_from=date(2026, 1, 1)),
        )
        demo_db_02 = await cat.create_product(
            ProductCreate(
                vendor_id=demo_vendor_02.id,
                name="Демо-СУБД 02",
                edition="14",
                licensing_model=LicensingModel.PER_CORE,
                price_currency="RUB",
                is_russian_registry=True,
            )
        )
        await cat.add_price(
            demo_db_02.id,
            PriceItemCreate(metric="ядро", price=Decimal("35000"), valid_from=date(2026, 1, 1)),
        )
        demo_db_03 = await cat.create_product(
            ProductCreate(
                vendor_id=demo_vendor_03.id,
                name="Демо-СУБД 03",
                edition="Enterprise",
                licensing_model=LicensingModel.PER_CORE,
                price_currency="USD",
            )
        )
        await cat.add_price(
            demo_db_03.id,
            PriceItemCreate(
                metric="core", price=Decimal("10000"), currency="USD", valid_from=date(2026, 1, 1)
            ),
        )

        # --- Проект (через сервис: генерация кода, стадии, аудит) ---
        proj_svc = ProjectService(session)
        project = await proj_svc.create(
            ProjectCreate(
                name="Импортозамещение СУБД и ОС в Демо-клиент 01",
                client_id=client01.id,
                type=ProjectType.IMPLEMENTATION,
                manager_id=pm.id,
                planned_start=date(2026, 2, 1),
                planned_end=date(2026, 11, 30),
                budget_revenue=Decimal("8400000"),
                contract_ref="DEMO-CONTRACT",
            ),
            actor_id=admin.id,
        )
        # presale -> survey -> design -> implementation (история переходов)
        for to_stage in (Stage.SURVEY, Stage.DESIGN, Stage.IMPLEMENTATION):
            await proj_svc.change_stage(project.id, to_stage, None, actor_id=pm.id)

        await proj_svc.add_member(
            project.id,
            MemberCreate(
                user_id=eng.id, role=ProjectMemberRole.ENGINEER, bill_rate=Decimal("3200")
            ),
        )
        await proj_svc.add_member(
            project.id,
            MemberCreate(
                user_id=eng2.id, role=ProjectMemberRole.ENGINEER, bill_rate=Decimal("2800")
            ),
        )
        await proj_svc.add_member(
            project.id,
            MemberCreate(
                user_id=users["pm.demo"].id, role=ProjectMemberRole.PM, bill_rate=Decimal("4200")
            ),
        )
        await proj_svc.add_member(
            project.id,
            MemberCreate(
                user_id=users["presale.demo"].id,
                role=ProjectMemberRole.PRESALE,
                bill_rate=Decimal("3500"),
            ),
        )

        # --- Задачи ---
        task_svc = TaskService(session)
        t_survey = await task_svc.create(
            project.id,
            TaskCreate(
                name="Обследование ИТ-инфраструктуры",
                stage=Stage.SURVEY,
                planned_hours=Decimal("80"),
                assignee_id=eng.id,
            ),
        )
        t_migrate = await task_svc.create(
            project.id,
            TaskCreate(
                name="Миграция БД на Демо-СУБД 02",
                stage=Stage.IMPLEMENTATION,
                planned_hours=Decimal("200"),
                assignee_id=eng.id,
            ),
        )
        await task_svc.create(
            project.id,
            TaskCreate(
                name="Внедрение Демо-ОС 01 на серверах",
                stage=Stage.IMPLEMENTATION,
                planned_hours=Decimal("160"),
                assignee_id=eng2.id,
            ),
        )

        # --- Вехи ---
        await proj_svc.add_milestone(
            project.id,
            MilestoneCreate(
                name="Подписание ТЗ",
                milestone_date=date(2026, 3, 15),
                is_payment=True,
                amount=Decimal("1400000"),
            ),
        )
        await proj_svc.add_milestone(
            project.id,
            MilestoneCreate(
                name="Ввод в опытную эксплуатацию",
                milestone_date=date(2026, 9, 1),
                is_payment=True,
                amount=Decimal("3500000"),
            ),
        )

        # --- Трудозатраты ---
        ts = TimesheetService(session)
        # Демо-инженер 01: прошлая неделя (01–05.06) полностью утверждена.
        approved_ids = []
        for day in range(1, 6):
            e = await ts.create_entry(
                eng,
                TimeEntryCreate(
                    project_id=project.id,
                    task_id=t_migrate.id,
                    work_date=date(2026, 6, day),
                    hours=Decimal("8"),
                    comment="Миграция схем и перенос данных в Демо-СУБД 02",
                ),
            )
            approved_ids.append(e.id)
        await ts.submit_week(eng, date(2026, 6, 1))
        await ts.approve(pm, approved_ids)
        # Демо-инженер 01: текущая неделя — черновик (08–09.06).
        for day in (8, 9):
            await ts.create_entry(
                eng,
                TimeEntryCreate(
                    project_id=project.id, task_id=t_survey.id, work_date=date(2026, 6, day),
                    hours=Decimal("7.5"), comment="Подготовка отчёта об обследовании",
                ),
            )
        # Демо-инженер 02: текущая неделя отправлена на утверждение (ждёт РП).
        for day in (8, 9, 10):
            await ts.create_entry(
                eng2,
                TimeEntryCreate(
                    project_id=project.id, work_date=date(2026, 6, day),
                    hours=Decimal("8"), comment="Развёртывание Демо-ОС 01 на тестовом контуре",
                ),
            )
        await ts.submit_week(eng2, date(2026, 6, 8))

        # --- Ресурсное планирование (тепловая карта) ---
        res = ResourceService(session)
        # недели: 08.06, 15.06, 22.06, 29.06 — разная загрузка (пере/недо/норма)
        plan = {
            eng.id: [44, 40, 20, 40],
            eng2.id: [40, 48, 32, 8],
            users["pm.demo"].id: [16, 20, 24, 12],
        }
        weeks = [date(2026, 6, 8), date(2026, 6, 15), date(2026, 6, 22), date(2026, 6, 29)]
        for uid, hours_list in plan.items():
            for wk, h in zip(weeks, hours_list, strict=True):
                await res.upsert_plan(
                    ResourcePlanUpsert(
                        user_id=uid, project_id=project.id, week_start=wk,
                        planned_hours=Decimal(str(h)),
                    )
                )

        # --- Портал Заказчика: ожидания работ (блокеры) ---
        cust = CustomerService(session)
        await cust.create_action_item(
            project.id,
            ActionItemCreate(
                title="Предоставить документы по текущей инфраструктуре",
                description="Для стадии обследования нужны схемы сети, перечень серверов и СУБД, "
                "свидетельства о правах на используемое ПО.",
                stage=Stage.SURVEY,
                responsible_name="Демо Пользователь 11",
                responsible_email="client.demo@client01.example.com",
                responsible_user_id=client_user.id,
                due_date=datetime(2026, 6, 20, tzinfo=UTC),
            ),
            actor_id=pm.id,
        )
        accepted_item = await cust.create_action_item(
            project.id,
            ActionItemCreate(
                title="Выделить тестовый контур для Демо-ОС 01",
                description="Виртуальные машины для развёртывания тестового стенда.",
                stage=Stage.SURVEY,
                responsible_name="ИТ-отдел Демо-клиент 01",
                responsible_user_id=client_user.id,
            ),
            actor_id=pm.id,
        )
        await cust.accept(accepted_item.id, actor_id=pm.id)

        # --- Расчёт ТКП ---
        quote_svc = QuoteService(session)
        quote = await quote_svc.create(
            project.id,
            QuoteCreate(
                title="ТКП на импортозамещение",
                currency_rates={"USD": "92.00", "EUR": "99.00"},
                currency_buffer_pct=Decimal("2"),
            ),
            actor_id=users["presale.demo"].id,
        )
        await quote_svc.add_line(
            quote.id,
            QuoteLineInput(
                kind=QuoteLineKind.LICENSE, name="Демо-ОС 01 (Сервер)", product_id=demo_os_01.id,
                licensing_model=LicensingModel.PER_SERVER, metric="сервер", currency="RUB",
                qty=Decimal("10"), unit_price=Decimal("42000"),
                partner_discount_pct=Decimal("30"), client_discount_pct=Decimal("10"),
            ),
        )
        await quote_svc.add_line(
            quote.id,
            QuoteLineInput(
                kind=QuoteLineKind.LICENSE, name="Демо-СУБД 02", product_id=demo_db_02.id,
                licensing_model=LicensingModel.PER_CORE, metric="ядро", currency="RUB",
                qty=Decimal("32"), unit_price=Decimal("35000"),
                partner_discount_pct=Decimal("35"), client_discount_pct=Decimal("15"),
            ),
        )
        await quote_svc.add_line(
            quote.id,
            QuoteLineInput(
                kind=QuoteLineKind.WORK, name="Внедрение, миграция, обучение",
                qty=Decimal("600"), unit_price=Decimal("3200"), unit_cost=Decimal("1300"),
            ),
        )
        await quote_svc.add_line(
            quote.id,
            QuoteLineInput(
                kind=QuoteLineKind.SUBCONTRACT, name="Монтаж СКС (субподряд)",
                qty=Decimal("1"), unit_price=Decimal("245000"), unit_cost=Decimal("196000"),
            ),
        )
        await quote_svc.set_status(quote.id, QuoteStatus.SENT, actor_id=users["presale.demo"].id)

        # Вторая версия (демонстрация версионирования).
        await quote_svc.clone_new_version(quote.id, actor_id=users["presale.demo"].id)

        # --- Финансы: бюджет и факт ---
        fin = FinanceService(session)
        await fin.upsert_budget(
            project.id,
            BudgetUpsert(
                planned_revenue=Decimal("8400000"),
                planned_costs={
                    "payroll": "3150000",
                    "licenses": "1400000",
                    "subcontract": "350000",
                    "travel": "140000",
                    "other": "210000",
                },
            ),
            actor_id=admin.id,
        )
        await fin.add_actual(
            project.id,
            ActualCostCreate(
                category=CostCategory.LICENSES, amount=Decimal("1295000"),
                occurred_on=date(2026, 5, 20), description="Закупка лицензий Демо-СУБД 02",
            ),
            actor_id=users["finance.demo"].id,
        )
        await fin.add_actual(
            project.id,
            ActualCostCreate(
                category=CostCategory.TRAVEL, amount=Decimal("101500"),
                occurred_on=date(2026, 5, 28), description="Командировка на площадку заказчика",
            ),
            actor_id=users["finance.demo"].id,
        )

        # Отклонённый таймшит (для демонстрации отказа + уведомления исполнителю).
        rejected_entry = await ts.create_entry(
            eng2,
            TimeEntryCreate(
                project_id=project.id, work_date=date(2026, 5, 26),
                hours=Decimal("8"), comment="Прочие работы",
            ),
        )
        await ts.submit_week(eng2, date(2026, 5, 25))
        await ts.reject(
            pm, [rejected_entry.id], "Уточните, по какой задаче списаны часы"
        )

        # ================= Дополнительные проекты (полный воркфлоу) =================
        pm2 = users["pm2.demo"]
        eng3 = users["eng3.demo"]
        eng4 = users["eng4.demo"]
        finance_id = users["finance.demo"].id
        presale_id = users["presale.demo"].id

        # --- Проект 2: пилот BI, демо-клиент 02, стадия «проектирование» ---
        p2 = await proj_svc.create(
            ProjectCreate(
                name="Пилот BI-аналитики, Демо-клиент 02",
                client_id=client02.id,
                type=ProjectType.PILOT,
                manager_id=pm2.id,
                planned_start=date(2026, 4, 1),
                planned_end=date(2026, 8, 31),
                budget_revenue=Decimal("3150000"),
                contract_ref="DEMO-CONTRACT",
            ),
            actor_id=admin.id,
        )
        p2 = await _advance(proj_svc, p2, Stage.DESIGN, admin.id)
        await proj_svc.add_member(
            p2.id,
            MemberCreate(
                user_id=eng4.id, role=ProjectMemberRole.ENGINEER, bill_rate=Decimal("2700")
            ),
        )
        t2 = await task_svc.create(
            p2.id,
            TaskCreate(
                name="Проектирование витрин данных",
                stage=Stage.DESIGN,
                planned_hours=Decimal("120"),
                assignee_id=eng4.id,
            ),
        )
        ids2 = []
        for d in range(1, 6):
            e = await ts.create_entry(
                eng4,
                TimeEntryCreate(
                    project_id=p2.id, task_id=t2.id, work_date=date(2026, 6, d),
                    hours=Decimal("8"), comment="Проектирование витрин",
                ),
            )
            ids2.append(e.id)
        await ts.submit_week(eng4, date(2026, 6, 1))
        await ts.approve(pm2, ids2)
        await fin.upsert_budget(
            p2.id,
            BudgetUpsert(
                planned_revenue=Decimal("3150000"),
                planned_costs={
                    "payroll": "1260000", "licenses": "420000",
                    "subcontract": "0", "travel": "70000", "other": "70000",
                },
            ),
            actor_id=admin.id,
        )
        await fin.add_actual(
            p2.id,
            ActualCostCreate(
                category=CostCategory.LICENSES, amount=Decimal("385000"),
                occurred_on=date(2026, 5, 15), description="Лицензии BI-платформы",
            ),
            actor_id=finance_id,
        )
        q2 = await quote_svc.create(
            p2.id, QuoteCreate(title="ТКП пилот BI"), actor_id=presale_id
        )
        await quote_svc.add_line(
            q2.id,
            QuoteLineInput(
                kind=QuoteLineKind.WORK, name="Пилотное внедрение и обучение",
                qty=Decimal("400"), unit_price=Decimal("2900"), unit_cost=Decimal("1200"),
            ),
        )
        await cust.create_action_item(
            p2.id,
            ActionItemCreate(
                title="Предоставить выгрузку продаж за 12 месяцев",
                description="Обезличенные данные продаж для построения витрин.",
                stage=Stage.DESIGN,
                responsible_name="Демо Пользователь 12",
                responsible_user_id=client2_user.id,
                due_date=datetime(2026, 6, 25, tzinfo=UTC),
            ),
            actor_id=pm2.id,
        )

        # --- Проект 3: демо-клиент 03, стадия «обследование», В ЗОНЕ РИСКА ---
        p3 = await proj_svc.create(
            ProjectCreate(
                name="Импортозамещение АБС, Демо-клиент 03",
                client_id=client03.id,
                type=ProjectType.IMPLEMENTATION,
                manager_id=pm.id,
                planned_start=date(2026, 3, 1),
                planned_end=date(2026, 12, 1),
                budget_revenue=Decimal("2100000"),
                contract_ref="DEMO-CONTRACT",
            ),
            actor_id=admin.id,
        )
        p3 = await _advance(proj_svc, p3, Stage.SURVEY, admin.id)
        await proj_svc.add_member(
            p3.id,
            MemberCreate(
                user_id=eng3.id, role=ProjectMemberRole.ENGINEER, bill_rate=Decimal("2900")
            ),
        )
        t3 = await task_svc.create(
            p3.id,
            TaskCreate(
                name="Обследование АБС",
                stage=Stage.SURVEY,
                planned_hours=Decimal("40"),
                assignee_id=eng3.id,
            ),
        )
        ids3 = []
        for d in range(1, 6):  # 50 ч факт против 40 план → перерасход часов
            e = await ts.create_entry(
                eng3,
                TimeEntryCreate(
                    project_id=p3.id, task_id=t3.id, work_date=date(2026, 6, d),
                    hours=Decimal("10"), comment="Обследование АБС",
                ),
            )
            ids3.append(e.id)
        await ts.submit_week(eng3, date(2026, 6, 1))
        await ts.approve(pm, ids3)
        await fin.upsert_budget(
            p3.id,
            BudgetUpsert(
                planned_revenue=Decimal("2100000"),
                planned_costs={
                    "payroll": "560000", "licenses": "1050000",
                    "subcontract": "350000", "travel": "70000", "other": "70000",
                },
            ),
            actor_id=admin.id,
        )
        # Факт превышает план → маржа отрицательная (проект подсветится как риск).
        await fin.add_actual(
            p3.id,
            ActualCostCreate(
                category=CostCategory.LICENSES, amount=Decimal("1750000"),
                occurred_on=date(2026, 5, 10), description="Закупка лицензий АБС",
            ),
            actor_id=finance_id,
        )
        await fin.add_actual(
            p3.id,
            ActualCostCreate(
                category=CostCategory.SUBCONTRACT, amount=Decimal("840000"),
                occurred_on=date(2026, 5, 20), description="Субподряд по интеграции",
            ),
            actor_id=finance_id,
        )

        # --- Проект 4: демо-клиент 04, поддержка, стадия «поддержка» ---
        p4 = await proj_svc.create(
            ProjectCreate(
                name="Сопровождение WMS, Демо-клиент 04",
                client_id=client04.id,
                type=ProjectType.SUPPORT,
                manager_id=pm2.id,
                planned_start=date(2026, 1, 1),
                planned_end=date(2026, 12, 31),
                budget_revenue=Decimal("1260000"),
                contract_ref="DEMO-CONTRACT",
            ),
            actor_id=admin.id,
        )
        p4 = await _advance(proj_svc, p4, Stage.SUPPORT, admin.id)
        await proj_svc.add_member(
            p4.id,
            MemberCreate(
                user_id=eng4.id, role=ProjectMemberRole.ENGINEER, bill_rate=Decimal("2500")
            ),
        )
        await fin.upsert_budget(
            p4.id,
            BudgetUpsert(
                planned_revenue=Decimal("1260000"),
                planned_costs={"payroll": "630000", "other": "70000"},
            ),
            actor_id=admin.id,
        )

        # --- Проект 5: демо-клиент 01, стадия «пресейл», ТКП в работе ---
        p5 = await proj_svc.create(
            ProjectCreate(
                name="Пресейл: ВКС-платформа для Демо-клиент 01",
                client_id=client01.id,
                type=ProjectType.PRESALE,
                manager_id=presale_id,
                budget_revenue=Decimal("0"),
            ),
            actor_id=admin.id,
        )
        q5 = await quote_svc.create(
            p5.id,
            QuoteCreate(
                title="ТКП ВКС-платформа",
                currency_rates={"USD": "92.00", "EUR": "99.00"},
                currency_buffer_pct=Decimal("3"),
            ),
            actor_id=presale_id,
        )
        await quote_svc.add_line(
            q5.id,
            QuoteLineInput(
                kind=QuoteLineKind.LICENSE, name="ВКС-сервер (импортный)",
                licensing_model=LicensingModel.PER_SERVER, currency="USD",
                qty=Decimal("3"), unit_price=Decimal("5600"),
                partner_discount_pct=Decimal("20"), client_discount_pct=Decimal("5"),
            ),
        )
        await quote_svc.add_line(
            q5.id,
            QuoteLineInput(
                kind=QuoteLineKind.WORK, name="Пусконаладка ВКС",
                qty=Decimal("160"), unit_price=Decimal("3500"), unit_cost=Decimal("1400"),
            ),
        )

        # --- Проект 6: завершённый (для демонстрации закрытого жизненного цикла) ---
        p6 = await proj_svc.create(
            ProjectCreate(
                name="Внедрение СЭД, Демо-клиент 02 (завершён)",
                client_id=client02.id,
                type=ProjectType.IMPLEMENTATION,
                manager_id=pm2.id,
                planned_start=date(2025, 6, 1),
                planned_end=date(2025, 12, 20),
                budget_revenue=Decimal("4200000"),
            ),
            actor_id=admin.id,
        )
        p6 = await _advance(proj_svc, p6, Stage.CLOSED, admin.id)  # статус → closed

        # --- Доп. ресурсные планы (богаче тепловая карта) ---
        weeks2 = [date(2026, 6, 8), date(2026, 6, 15), date(2026, 6, 22), date(2026, 6, 29)]
        extra_plan = {
            (eng3.id, p3.id): [48, 40, 24, 16],
            (eng4.id, p2.id): [40, 36, 40, 8],
            (pm2.id, p2.id): [12, 16, 20, 10],
        }
        for (uid, pid), hours_list in extra_plan.items():
            for wk, h in zip(weeks2, hours_list, strict=True):
                await res.upsert_plan(
                    ResourcePlanUpsert(
                        user_id=uid, project_id=pid, week_start=wk,
                        planned_hours=Decimal(str(h)),
                    )
                )

        # --- Реестр рисков (матрица вероятность×влияние, портфель) ---
        risk_svc = RiskService(session)
        # Проект 1 (демо-клиент 01, внедрение) — набор для портфеля/матрицы.
        await risk_svc.create(
            project.id,
            RiskCreate(
                title="Несовместимость legacy-приложений с Демо-СУБД 02",
                description="Часть прикладного ПО Заказчика использует специфичные "
                "функции Oracle — потребуется доработка.",
                category=RiskCategory.TECHNICAL,
                probability=3,
                impact=3,
                response_strategy=RiskResponse.MITIGATE,
                mitigation_plan="Аудит SQL-кода на стадии обследования, план миграции "
                "несовместимых объектов, пилот на тестовом контуре.",
                owner_id=eng.id,
                due_date=date(2026, 7, 15),
            ),
            actor_id=pm.id,
        )
        await risk_svc.create(
            project.id,
            RiskCreate(
                title="Задержка предоставления тестового контура Заказчиком",
                description="Без тестовых ВМ невозможно начать миграцию в срок.",
                category=RiskCategory.EXTERNAL,
                probability=3,
                impact=2,
                response_strategy=RiskResponse.TRANSFER,
                mitigation_plan="Зафиксировать срок в плане-графике, эскалация куратору.",
                owner_id=users["pm.demo"].id,
                due_date=date(2026, 6, 25),
            ),
            actor_id=pm.id,
        )
        await risk_svc.create(
            project.id,
            RiskCreate(
                title="Рост курса валют по импортному оборудованию",
                category=RiskCategory.BUDGET,
                probability=2,
                impact=3,
                response_strategy=RiskResponse.MITIGATE,
                mitigation_plan="Валютный буфер в ТКП, фиксация цен у поставщика.",
                owner_id=users["finance.demo"].id,
            ),
            actor_id=pm.id,
        )
        mitigating = await risk_svc.create(
            project.id,
            RiskCreate(
                title="Недостаток инженеров с компетенцией Демо-ОС 01",
                category=RiskCategory.RESOURCE,
                probability=2,
                impact=2,
                response_strategy=RiskResponse.MITIGATE,
                mitigation_plan="Обучение команды, привлечение субподряда на пик.",
                owner_id=eng2.id,
            ),
            actor_id=pm.id,
        )
        await risk_svc.update(
            mitigating.id, RiskUpdate(status=RiskStatus.MITIGATING), actor_id=pm.id
        )
        closed_risk = await risk_svc.create(
            project.id,
            RiskCreate(
                title="Неполный комплект исходной документации",
                category=RiskCategory.SCOPE,
                probability=1,
                impact=2,
                response_strategy=RiskResponse.ACCEPT,
                owner_id=eng.id,
            ),
            actor_id=pm.id,
        )
        await risk_svc.update(
            closed_risk.id, RiskUpdate(status=RiskStatus.CLOSED), actor_id=pm.id
        )

        # Проект 2 — два риска.
        await risk_svc.create(
            p2.id,
            RiskCreate(
                title="Срыв сроков интеграции с внешними системами",
                category=RiskCategory.SCHEDULE,
                probability=3,
                impact=3,
                response_strategy=RiskResponse.MITIGATE,
                mitigation_plan="Ранний прототип интеграции, буфер в графике.",
                owner_id=eng4.id,
            ),
            actor_id=admin.id,
        )
        await risk_svc.create(
            p2.id,
            RiskCreate(
                title="Изменение требований со стороны бизнеса",
                category=RiskCategory.SCOPE,
                probability=2,
                impact=2,
                response_strategy=RiskResponse.MITIGATE,
                owner_id=pm2.id,
            ),
            actor_id=admin.id,
        )
        # Проект 3 — один высокий риск.
        await risk_svc.create(
            p3.id,
            RiskCreate(
                title="Зависимость от поставки оборудования вендора",
                category=RiskCategory.EXTERNAL,
                probability=2,
                impact=3,
                response_strategy=RiskResponse.TRANSFER,
                mitigation_plan="Резервный поставщик, штрафные санкции в договоре.",
                owner_id=eng3.id,
            ),
            actor_id=admin.id,
        )

        # Приостановленный проект (статус on_hold) — для фильтров портфеля.
        await proj_svc.update(
            p4.id, ProjectUpdate(status=ProjectStatus.ON_HOLD), actor_id=admin.id
        )

        await session.commit()
        print(
            "Готово. Проекты: "
            f"{project.code}, {p2.code}, {p3.code}, {p4.code}, {p5.code}, {p6.code}. "
            "Таймшиты (вкл. отклонённый), ТКП, финансы (вкл. риск), ресурсы, портал — заполнены."
        )


if __name__ == "__main__":
    asyncio.run(main())
