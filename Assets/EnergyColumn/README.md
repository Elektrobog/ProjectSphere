# Sci-Fi Energy Column — перенос в Unity (URP)

Колонна с текущей энергией из Blender. Важный принцип: **геометрия едет через
FBX, а анимация энергии живёт в шейдере Unity** — никаких аниматоров и запечённых
кадров не нужно, работает на любой платформе.

## Что лежит в этой папке

| Файл | Назначение |
|---|---|
| BlenderSciFiColumn.py | Генератор колонны для Blender 3.x–5.x + авто-экспорт FBX |
| SciFiEnergyColumn.shader | Шейдер энергии под URP (те же бегущие капли, что в Blender) |

## Шаг 1. Blender

1. Blender -> вкладка Scripting -> Open -> выбрать BlenderSciFiColumn.py -> Run Script.
2. В параметрах вверху стоит EXPORT_FBX = True — после генерации скрипт сам:
   - конвертирует кривые (стекло, энергия) в обычные меши,
   - поставит пивот каждого меша в центр (шейдер берёт фазу течения из позиции объекта),
   - сохранит SciFiEnergyColumn.fbx рядом с .blend-файлом
     (если .blend не сохранён — в домашнюю папку).
3. Посмотри превью: Space (Play) в Material/Rendered Preview — энергия течёт.

## Шаг 2. Unity — импорт модели

1. Перетащи SciFiEnergyColumn.fbx в окно Project (внутрь Assets).
2. Выбери модель -> в Inspector проверь вкладку Model:
   - Scale Factor = 1 (если объект появился гигантским — включи Convert Units),
   - Normals = Import.
3. Unity сам создаст материалы с именами из Blender.

## Шаг 3. Материалы

Открой каждый материал (клик в Project) и назначь шейдер:

| Материал (имя из FBX) | Шейдер и настройки |
|---|---|
| Energy - Flow Vertical | ProjectSphere/EnergyFlow, Flow Mode = Vertical Tube |
| Energy - Flow Ring | ProjectSphere/EnergyFlow, Flow Mode = Horizontal Ring |
| Transparent Glass - Tubes | URP/Lit: Surface Type = Transparent, Smoothness ~0.95, Base Color голубоватый, Alpha ~0.15 |
| Graphite Blue - Armor | URP/Lit, Metallic 0.1, Smoothness ~0.6 |
| Inner Body - Dark Blue | URP/Lit, тёмно-синий |

У Energy-материалов цвет уже задан в шейдере (Mid/Hot Color), Base Color менять не нужно.

## Шаг 4. Bloom — чтобы энергия светилась

1. В сцене: GameObject -> Volume -> Global Volume.
2. В компоненте Volume -> Add Override -> Post Processing -> поставить галочку Bloom:
   Threshold ~0.8, Intensity 1–2.
3. В URP Asset (находится в Assets/Settings): Post Processing = ON, HDR = ON.

## Настройки шейдера энергии

| Параметр | Что делает |
|---|---|
| Flow Speed | Скорость течения |
| Blob Length | Длина капли вдоль трубки |
| Blob Contrast | Резкость капель (больше — темнее паузы) |
| Min Brightness | Свечение пауз: 0 = тёмные паузы, 0.05–0.1 = лёгкий фон |
| Spark Brightness | Быстрые искры поверх капель (0 = выключить) |
| Overall Brightness | Общая яркость — должна пробивать порог Bloom |

## Важно

- Анимация живёт в шейдере (_Time) — Animator не нужен, дёшево для мобилок.
- В шейдере стоит DisableBatching: у каждой трубки своя фаза течения из позиции
  объекта, поэтому Static Batching для трубок включать нельзя.
- Скрипт Blender-генератора запускается в новом файле: он очищает сцену.
