"""
Sci-Fi Energy Column — версия с эффектом «текущей» энергии
Габарит по умолчанию: ВЫСОТА 50 м, ДИАМЕТР 10 м (все размеры в блоке
ПАРАМЕТРЫ пропорционально согласованы под эту башню).
Совместим с Blender 3.x / 4.x / 5.x
Запуск: Scripting -> Open -> Run Script

Что изменилось против прошлой версии:
- Энергия теперь НЕ сплошной светящийся стержень, а поток отдельных
  импульсов-«капель», бегущих по трубке с тёмными паузами между ними.
  За это отвечают: ENERGY_TRAIL = 0 (тёмные паузы), крупный
  ENERGY_STREAK_SIZE (капли видны глазу), ENERGY_BLOB_CONTRAST
  (резкие края капель) и толстый стержень ENERGY_RADIUS.
- Анимация времени переведена с драйвера на узел Scene Time — он
  анимируется по кадру всегда, без авто-запуска Python (драйвер оставлен
  как запасной вариант для очень старых сборок).
- Яркость капель усилена (ENERGY_BRIGHTNESS), чтобы в Bloom/Glare
  светились только импульсы, а не вся трубка.

Чтобы увидеть течение: нажмите Play (Space) во вьюпорте в режиме
Material/Rendered Preview либо рендерьте анимацию — узел читает кадр сцены.

Для переноса в игру (Unity): EXPORT_FBX = True — скрипт сам конвертирует
кривые в меши и сохраняет FBX рядом с .blend. Течение энергии в Unity
живёт в шейдере (SciFiEnergyColumn.shader под URP), FBX несёт
только геометрию и имена материалов.
"""

import bpy
import bmesh
import math
import random

# ============================================================
# 1. ПАРАМЕТРЫ
# ============================================================

HEIGHT = 50.0
DIAMETER = 10.0

VERTICAL_LINES = 32          # плит по окружности = и вертикальных трубок;
                              # 32 -> плита ~0.8 м шириной, трубка примерно раз в метр
HORIZONTAL_ROWS = 40         # рядов плит по высоте (кольца — по границам рядов);
                              # 40 -> плита ~1.0 м высотой, кольцо каждые 1.25 м

GROOVE_WIDTH = 0.22          # ширина углублений (в них лежат трубки)
PLATE_DEPTH = 0.5            # толщина плит
PLATE_ARC_SEGMENTS = 6       # сглаженность дуги плиты

BEVEL_WIDTH = 0.10
BEVEL_SEGMENTS = 3

SEPARATE_PLATES = False      # True = каждая плита отдельным объектом (медленнее,
                             # но можно анимировать плиты по одной)

# Стеклянные трубки
GLASS_RADIUS = 0.09           # радиус трубки 9 см: видна издалека, но не занимает всё
GLASS_ROUGHNESS = 0.05
CIRCLE_SEGMENTS = 128        # сегментов кольца (31 м окружности — 128 достаточно)
TUBE_SINK = 0.04             # насколько трубка утоплена относительно поверхности плит
GLASS_REFLECT = 0.10         # базовая отражающая способность стекла
GLASS_EDGE = 3.0              # яркость бликов по краям трубки

# Энергия: зелёные импульсы-«капли», бегущие внутри прозрачных трубок
ENERGY_RADIUS = 0.06          # радиус стержня энергии (6 см внутри 9 см стекла)
ENERGY_COLOR = (0.0, 1.0, 0.1, 1.0)
ENERGY_POWER = 1.0            # общая яркость энергии
ENERGY_BRIGHTNESS = 3.0       # усиление свечения: капли пробивают порог Bloom,
                              # паузы между ними — нет
ENERGY_ANIMATED = True        # течение энергии (узел Scene Time, надёжнее драйвера)
ENERGY_PULSE_PERIOD = 5.0     # период быстрых искр на вертикальных трубках (м)
ENERGY_RING_PULSES = 6        # волн искр на каждом горизонтальном кольце
ENERGY_PULSE_SPEED = 0.1      # скорость течения капель (безразмерных единиц/кадр)
                              # 0.1 при STREAK 2.0 ~= 5 м/с при 24 fps —
                              # капля проходит 50-метровую башню за ~10 секунд
ENERGY_TRAIL = 0.0            # яркость ПАУЗ между импульсами (0 = тёмные промежутки;
                              # именно старое значение 0.4 делало из энергии «палку»)
ENERGY_STREAK_SIZE = 2.0      # длина капли вдоль трубки (м): 2 м на 50-метровой башне
                              # (в Unity: ④ Длина капли = 2.0 в материале вертикалей)
ENERGY_FILAMENTS = 3          # сколько прожилок вокруг стержня энергии (2-4)
ENERGY_BLOB_CONTRAST = 1.7    # контраст капель: >1 = резче капли и темнее фон
ENERGY_SPARKS = 1.5           # тонкие быстрые искры-«бусины» поверх капель (0 = выкл)

# Свечение вокруг импульсов
ENERGY_HALO = False          # ореол-геометрия (по умолчанию выключена, свечение даёт Glare)
ENERGY_HALO_RADIUS = 0.15
ENERGY_HALO_OPACITY = 0.25
ENERGY_HALO_STRENGTH = 1.0
ENERGY_GLOW_POST = True      # Bloom/Glare в постобработке
ENERGY_GLOW_THRESHOLD = 0.8  # порог свечения (меньше = светится больше)

# Материалы
PLATE_COLOR = (0.12, 0.17, 0.23, 1.0)
PLATE_ROUGHNESS = 0.42
PLATE_METALLIC = 0.10
GLASS_COLOR = (0.75, 0.88, 1.0, 1.0)    # холодный оттенок бликов стекла
CORE_COLOR = (0.025, 0.045, 0.07, 1.0)

CREATE_INNER_BODY = True
SETUP_SCENE = True           # тёмный мир, камера и свет

# Экспорт в игру (Unity)
EXPORT_FBX = True             # после генерации: кривые -> меши и сохранить FBX
FBX_FILENAME = "SciFiEnergyColumn.fbx"   # файл появится рядом с .blend
                                         # (если .blend не сохранён — в домашней папке)


# ============================================================
# 2. ПРОВЕРКА ПАРАМЕТРОВ И РАСЧЁТЫ
# ============================================================

R = DIAMETER / 2
INNER_R = R - PLATE_DEPTH
STEP_ANGLE = 2 * math.pi / VERTICAL_LINES
ANGLE_GAP = GROOVE_WIDTH / R
ROW_HEIGHT = HEIGHT / HORIZONTAL_ROWS

assert VERTICAL_LINES >= 3, "VERTICAL_LINES должно быть >= 3"
assert HORIZONTAL_ROWS >= 1, "HORIZONTAL_ROWS должно быть >= 1"
assert GROOVE_WIDTH < ROW_HEIGHT, "GROOVE_WIDTH больше высоты ряда — плиты исчезнут"
assert ANGLE_GAP < STEP_ANGLE, "GROOVE_WIDTH больше ширины плиты"
assert PLATE_DEPTH < R, "PLATE_DEPTH больше радиуса колонны"

ACTUAL_BEVEL = min(BEVEL_WIDTH, GROOVE_WIDTH * 0.3, PLATE_DEPTH * 0.3)
TUBE_RADIUS = min(GLASS_RADIUS, GROOVE_WIDTH * 0.46)
TUBE_DISTANCE = R - TUBE_RADIUS - TUBE_SINK

ENERGY_CORE_R = min(ENERGY_RADIUS, TUBE_RADIUS * 0.7)          # фактический радиус стержня
CROSS_FREQ = max(1.0, ENERGY_FILAMENTS / (2 * math.pi * ENERGY_CORE_R))
                                                               # частота шума поперёк трубки:
                                                               # ровно ENERGY_FILAMENTS прожилок
ENERGY_MATS = {}   # материалы энергии: True = вертикальные, False = кольца
HALO_MATS = {}     # материалы ореола
FLOW_RNG = random.Random(7)


# ============================================================
# 3. ОЧИСТКА СЦЕНЫ (без мусора в файле)
# ============================================================

def clear_scene():
    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")

    for obj in list(bpy.data.objects):
        bpy.data.objects.remove(obj, do_unlink=True)

    for col in list(bpy.data.collections):
        bpy.data.collections.remove(col)

    # чистим осиротевшие данные от прошлых запусков
    for datablocks in (bpy.data.meshes, bpy.data.curves, bpy.data.materials):
        for block in list(datablocks):
            if block.users == 0:
                datablocks.remove(block)


def make_collection(name):
    col = bpy.data.collections.new(name)
    bpy.context.scene.collection.children.link(col)
    return col


def move_to_collection(obj, collection):
    for col in list(obj.users_collection):
        col.objects.unlink(obj)
    collection.objects.link(obj)


# ============================================================
# 4. МАТЕРИАЛЫ
# ============================================================

def set_input(node, names, value):
    """Ставит значение сокета, пробуя разные имена (разные версии Blender)."""
    for name in names:
        if name in node.inputs:
            node.inputs[name].default_value = value
            return True
    return False


def make_principled(name, color, roughness, metallic=0.0):
    mat = bpy.data.materials.new(name)
    mat.diffuse_color = color
    mat.use_nodes = True
    bsdf = mat.node_tree.nodes.get("Principled BSDF")
    set_input(bsdf, ["Base Color"], color)
    set_input(bsdf, ["Roughness"], roughness)
    set_input(bsdf, ["Metallic"], metallic)
    return mat


def make_glass_material():
    """Прозрачное стекло: почти невидимо в центре, яркие блики по краям трубки."""
    mat = bpy.data.materials.new("Transparent Glass - Tubes")
    mat.use_nodes = True
    mat.diffuse_color = GLASS_COLOR
    enable_blend(mat)

    nt = mat.node_tree
    nt.nodes.clear()

    output = nt.nodes.new("ShaderNodeOutputMaterial")
    mix = nt.nodes.new("ShaderNodeMixShader")
    transparent = nt.nodes.new("ShaderNodeBsdfTransparent")
    glossy = nt.nodes.new("ShaderNodeBsdfGlossy")
    fresnel = nt.nodes.new("ShaderNodeFresnel")

    glossy.inputs["Color"].default_value = GLASS_COLOR
    glossy.inputs["Roughness"].default_value = GLASS_ROUGHNESS
    fresnel.inputs["IOR"].default_value = 1.45

    # доля отражения = база + fresnel * усиление (по краям трубки сильнее)
    fac = math_node(nt, "MULTIPLY_ADD", None, GLASS_EDGE, GLASS_REFLECT)
    fac.use_clamp = True

    nt.links.new(fresnel.outputs["Fac"], fac.inputs[0])
    nt.links.new(fac.outputs["Value"], mix.inputs["Fac"])
    nt.links.new(transparent.outputs["BSDF"], mix.inputs[1])
    nt.links.new(glossy.outputs["BSDF"], mix.inputs[2])
    nt.links.new(mix.outputs["Shader"], output.inputs["Surface"])

    return mat


def math_node(nt, op, v0=None, v1=None, v2=None):
    node = nt.nodes.new("ShaderNodeMath")
    node.operation = op
    for i, v in enumerate((v0, v1, v2)):
        if v is not None:
            node.inputs[i].default_value = v
    return node


def enable_blend(mat, dithered=False):
    """
    Прозрачность материала.
    dithered=False -> Blended: гладко, без зернистости (для стекла).
    dithered=True  -> Dithered/Hashed: без проблем сортировки, но с зерном.
    """
    if hasattr(mat, "blend_method"):                    # EEVEE Legacy
        try:
            mat.blend_method = "HASHED" if dithered else "BLEND"
        except (TypeError, AttributeError):
            pass
    if hasattr(mat, "surface_render_method"):           # EEVEE Next (4.2+)
        try:
            mat.surface_render_method = "DITHERED" if dithered else "BLENDED"
        except (TypeError, AttributeError):
            pass
    for attr in ("show_transparent_back", "use_transparency_overlap"):
        if hasattr(mat, attr):
            setattr(mat, attr, False)


def wave_node(nt, pos, time, offset, freq, speed):
    """Бегущая волна sin(pos*freq - time*speed + offset), значения -1..1."""
    f = math_node(nt, "MULTIPLY", None, freq)
    nt.links.new(pos, f.inputs[0])
    sp = math_node(nt, "MULTIPLY", None, speed)
    nt.links.new(time, sp.inputs[0])
    sub = math_node(nt, "SUBTRACT")
    nt.links.new(f.outputs["Value"], sub.inputs[0])
    nt.links.new(sp.outputs["Value"], sub.inputs[1])
    add = math_node(nt, "ADD")
    nt.links.new(sub.outputs["Value"], add.inputs[0])
    nt.links.new(offset, add.inputs[1])
    sine = math_node(nt, "SINE")
    nt.links.new(add.outputs["Value"], sine.inputs[0])
    return sine.outputs["Value"]


def make_time(nt):
    """
    «Время» = кадр * ENERGY_PULSE_SPEED.
    Основной путь — узел Scene Time (есть во всех Blender 3.x - 5.x):
    он обновляется по таймлайну всегда и не зависит от разрешений на
    запуск Python-драйверов. Если узла вдруг нет — запасной вариант через драйвер.
    """
    if not ENERGY_ANIMATED:
        return math_node(nt, "ADD", 0.0, 0.0).outputs["Value"]

    try:
        scene_time = nt.nodes.new("ShaderNodeSceneTime")
        t = math_node(nt, "MULTIPLY", None, ENERGY_PULSE_SPEED)
        nt.links.new(scene_time.outputs["Frame"], t.inputs[0])
        return t.outputs["Value"]
    except Exception:
        t = math_node(nt, "ADD", 0.0, 0.0)
        try:
            fcurve = t.inputs[0].driver_add("default_value")
            fcurve.driver.type = "SCRIPTED"
            fcurve.driver.expression = f"frame * {ENERGY_PULSE_SPEED}"
        except Exception as err:
            print("Не удалось создать время анимации энергии:", err)
        return t.outputs["Value"]


def build_flow_mask(nt, vertical):
    """
    Маска течения энергии (значения ~0..1, поверх добавляются искры):
    1) вытянутый вдоль трубки шум -> отдельные «капли», которые движутся
       вдоль трубки (вверх по вертикалям, по кругу на кольцах);
    2) MapRange + POWER сжимают контраст: между каплями наступают ТЁМНЫЕ
       паузы (ENERGY_TRAIL = 0) — поэтому трубка читается как поток,
       а не сплошная палка;
    3) поверх — тонкие быстрые искры-«бусины» (ENERGY_SPARKS).
    У каждой трубки своя фаза (custom property "flow_offset"), поэтому
    капли в соседних трубках не бегут синхронно.
    """
    coord = nt.nodes.new("ShaderNodeTexCoord")
    sep = nt.nodes.new("ShaderNodeSeparateXYZ")
    nt.links.new(coord.outputs["Object"], sep.inputs["Vector"])

    if vertical:
        pos_node = math_node(nt, "MULTIPLY", None, 2 * math.pi / ENERGY_PULSE_PERIOD)
        nt.links.new(sep.outputs["Z"], pos_node.inputs[0])
    else:
        angle = math_node(nt, "ARCTAN2")
        nt.links.new(sep.outputs["Y"], angle.inputs[0])
        nt.links.new(sep.outputs["X"], angle.inputs[1])
        pos_node = math_node(nt, "MULTIPLY", None, ENERGY_RING_PULSES)
        nt.links.new(angle.outputs["Value"], pos_node.inputs[0])
    pos = pos_node.outputs["Value"]

    time = make_time(nt)

    # индивидуальный сдвиг фазы каждой трубки
    attr = nt.nodes.new("ShaderNodeAttribute")
    attr.attribute_type = "OBJECT"
    attr.attribute_name = '["flow_offset"]'
    offset = attr.outputs["Fac"]

    # --- капли: шум, вытянутый вдоль трубки и сжатый поперёк ---
    size = ENERGY_STREAK_SIZE

    if vertical:
        # поперёк: ровно ENERGY_FILAMENTS прожилок по окружности стержня
        x = math_node(nt, "MULTIPLY", None, CROSS_FREQ)
        nt.links.new(sep.outputs["X"], x.inputs[0])
        y = math_node(nt, "MULTIPLY", None, CROSS_FREQ)
        nt.links.new(sep.outputs["Y"], y.inputs[0])
        # вдоль: вытянутые капли; вычитание времени = движение ВВЕРХ по трубке
        z = math_node(nt, "MULTIPLY", None, 1.0 / size)
        nt.links.new(sep.outputs["Z"], z.inputs[0])
        zt = math_node(nt, "SUBTRACT")
        nt.links.new(z.outputs["Value"], zt.inputs[0])
        nt.links.new(time, zt.inputs[1])
        zo = math_node(nt, "ADD")
        nt.links.new(zt.outputs["Value"], zo.inputs[0])
        nt.links.new(offset, zo.inputs[1])
        vec = nt.nodes.new("ShaderNodeCombineXYZ")
        nt.links.new(x.outputs["Value"], vec.inputs["X"])
        nt.links.new(y.outputs["Value"], vec.inputs["Y"])
        nt.links.new(zo.outputs["Value"], vec.inputs["Z"])
    else:
        # кольцо: ходим по окружности в пространстве шума, поэтому шов не появляется
        phi = math_node(nt, "MULTIPLY", None, size / TUBE_DISTANCE)
        nt.links.new(time, phi.inputs[0])
        a_t = math_node(nt, "SUBTRACT")
        nt.links.new(angle.outputs["Value"], a_t.inputs[0])
        nt.links.new(phi.outputs["Value"], a_t.inputs[1])
        a_o = math_node(nt, "ADD")
        nt.links.new(a_t.outputs["Value"], a_o.inputs[0])
        nt.links.new(offset, a_o.inputs[1])
        cosn = math_node(nt, "COSINE")
        nt.links.new(a_o.outputs["Value"], cosn.inputs[0])
        sinn = math_node(nt, "SINE")
        nt.links.new(a_o.outputs["Value"], sinn.inputs[0])
        k = TUBE_DISTANCE / size          # капля длиной ~size метров вдоль кольца
        kx = math_node(nt, "MULTIPLY", None, k)
        nt.links.new(cosn.outputs["Value"], kx.inputs[0])
        ky = math_node(nt, "MULTIPLY", None, k)
        nt.links.new(sinn.outputs["Value"], ky.inputs[0])
        vec = nt.nodes.new("ShaderNodeCombineXYZ")
        nt.links.new(kx.outputs["Value"], vec.inputs["X"])
        nt.links.new(ky.outputs["Value"], vec.inputs["Y"])

    noise = nt.nodes.new("ShaderNodeTexNoise")
    noise.noise_dimensions = "3D"
    noise.inputs["Scale"].default_value = 1.0
    noise.inputs["Detail"].default_value = 2.0
    noise.inputs["Roughness"].default_value = 0.5
    nt.links.new(vec.outputs["Vector"], noise.inputs["Vector"])

    # контраст 1: из мягкого шума делаем выраженные капли 0..1
    squeeze = nt.nodes.new("ShaderNodeMapRange")
    squeeze.inputs["From Min"].default_value = 0.38
    squeeze.inputs["From Max"].default_value = 0.68
    squeeze.inputs["To Min"].default_value = 0.0
    squeeze.inputs["To Max"].default_value = 1.0
    nt.links.new(noise.outputs["Fac"], squeeze.inputs["Value"])

    # контраст 2: POWER затачивает края капель (фон уходит в темноту)
    punch = math_node(nt, "POWER", None, ENERGY_BLOB_CONTRAST)
    nt.links.new(squeeze.outputs["Result"], punch.inputs[0])

    # паузы между каплями: подмешиваем ENERGY_TRAIL (0 = честные тёмные паузы)
    floor = nt.nodes.new("ShaderNodeMapRange")
    floor.inputs["From Min"].default_value = 0.0
    floor.inputs["From Max"].default_value = 1.0
    floor.inputs["To Min"].default_value = ENERGY_TRAIL
    floor.inputs["To Max"].default_value = 1.0
    nt.links.new(punch.outputs["Value"], floor.inputs["Value"])
    result = floor.outputs["Result"]

    # тонкие быстрые искры-«бусины» поверх капель
    if ENERGY_SPARKS > 0:
        spark = wave_node(nt, pos, time, offset, 5, 3.5)
        spark01 = math_node(nt, "MULTIPLY_ADD", None, 0.5, 0.5)
        nt.links.new(spark, spark01.inputs[0])
        sharp = math_node(nt, "POWER", None, 12.0)
        nt.links.new(spark01.outputs["Value"], sharp.inputs[0])
        boosted = math_node(nt, "MULTIPLY_ADD", None, ENERGY_SPARKS)
        nt.links.new(sharp.outputs["Value"], boosted.inputs[0])
        nt.links.new(result, boosted.inputs[2])
        result = boosted.outputs["Value"]

    return result


def make_energy_material(vertical):
    """Энергия внутри стекла: по трубке бегут яркие капли с тёмными паузами."""
    mat = bpy.data.materials.new("Energy - Flow " + ("Vertical" if vertical else "Ring"))
    mat.use_nodes = True
    mat.diffuse_color = ENERGY_COLOR

    nt = mat.node_tree
    nt.nodes.clear()

    flow = build_flow_mask(nt, vertical)

    output = nt.nodes.new("ShaderNodeOutputMaterial")
    emission = nt.nodes.new("ShaderNodeEmission")
    layer = nt.nodes.new("ShaderNodeLayerWeight")
    layer.inputs["Blend"].default_value = 0.3

    # объём: центр трубки ярче, к краям заметно темнее — у капель горячая сердцевина
    center = math_node(nt, "SUBTRACT", 1.0)
    nt.links.new(layer.outputs["Fresnel"], center.inputs[1])
    cpow = math_node(nt, "POWER", None, 1.5)
    nt.links.new(center.outputs["Value"], cpow.inputs[0])
    shade = math_node(nt, "MULTIPLY_ADD", None, 0.8, 0.2)
    nt.links.new(cpow.outputs["Value"], shade.inputs[0])

    bright = math_node(nt, "MULTIPLY")
    nt.links.new(flow, bright.inputs[0])
    nt.links.new(shade.outputs["Value"], bright.inputs[1])

    # цвет по яркости: паузы чёрные -> насыщенно-зелёные капли -> бело-зелёные сердцевины
    ramp = nt.nodes.new("ShaderNodeValToRGB")
    elements = ramp.color_ramp.elements
    elements[0].position = 0.0
    elements[0].color = (0.0, 0.0, 0.0, 1.0)
    elements[1].position = 1.0
    elements[1].color = (0.65, 1.0, 0.75, 1.0)
    mid = elements.new(0.5)
    mid.color = ENERGY_COLOR
    nt.links.new(bright.outputs["Value"], ramp.inputs["Fac"])

    # капли яркие (попадают в Bloom), паузы тёмные (не светятся вовсе)
    strength = math_node(nt, "MULTIPLY", None, ENERGY_POWER * ENERGY_BRIGHTNESS)
    nt.links.new(bright.outputs["Value"], strength.inputs[0])

    nt.links.new(ramp.outputs["Color"], emission.inputs["Color"])
    nt.links.new(strength.outputs["Value"], emission.inputs["Strength"])
    nt.links.new(emission.outputs["Emission"], output.inputs["Surface"])

    return mat


def make_halo_material(vertical):
    """Мягкий ореол, который есть только вокруг импульсов энергии."""
    mat = bpy.data.materials.new("Energy - Halo " + ("Vertical" if vertical else "Ring"))
    mat.use_nodes = True
    mat.diffuse_color = ENERGY_COLOR
    enable_blend(mat, dithered=True)

    nt = mat.node_tree
    nt.nodes.clear()

    mask = build_flow_mask(nt, vertical)

    output = nt.nodes.new("ShaderNodeOutputMaterial")
    mix = nt.nodes.new("ShaderNodeMixShader")
    transparent = nt.nodes.new("ShaderNodeBsdfTransparent")
    emission = nt.nodes.new("ShaderNodeEmission")
    layer = nt.nodes.new("ShaderNodeLayerWeight")

    layer.inputs["Blend"].default_value = 0.3

    invert = math_node(nt, "SUBTRACT", 1.0)                       # центр ярче, края прозрачнее
    power = math_node(nt, "POWER", None, 2.0)
    scale = math_node(nt, "MULTIPLY", None, ENERGY_HALO_OPACITY)
    glow = math_node(nt, "MULTIPLY")                              # маска импульса * форма ореола
    glow.use_clamp = True

    emission.inputs["Color"].default_value = ENERGY_COLOR
    emission.inputs["Strength"].default_value = ENERGY_POWER * ENERGY_HALO_STRENGTH

    nt.links.new(layer.outputs["Fresnel"], invert.inputs[1])
    nt.links.new(invert.outputs["Value"], power.inputs[0])
    nt.links.new(power.outputs["Value"], scale.inputs[0])
    nt.links.new(mask, glow.inputs[0])
    nt.links.new(scale.outputs["Value"], glow.inputs[1])
    nt.links.new(glow.outputs["Value"], mix.inputs["Fac"])
    nt.links.new(transparent.outputs["BSDF"], mix.inputs[1])
    nt.links.new(emission.outputs["Emission"], mix.inputs[2])
    nt.links.new(mix.outputs["Shader"], output.inputs["Surface"])

    return mat


# ============================================================
# 5. БРОНЕПЛИТЫ
# ============================================================

def add_plate_geometry(bm, row, column):
    """Добавляет одну плиту (дуговой блок) в bmesh."""
    a0 = column * STEP_ANGLE + ANGLE_GAP / 2
    a1 = (column + 1) * STEP_ANGLE - ANGLE_GAP / 2
    z0 = -HEIGHT / 2 + row * ROW_HEIGHT + GROOVE_WIDTH / 2
    z1 = -HEIGHT / 2 + (row + 1) * ROW_HEIGHT - GROOVE_WIDTH / 2

    verts = []
    for i in range(PLATE_ARC_SEGMENTS + 1):
        t = i / PLATE_ARC_SEGMENTS
        ang = a0 + (a1 - a0) * t
        c, s = math.cos(ang), math.sin(ang)
        verts.extend([
            bm.verts.new((R * c, R * s, z0)),
            bm.verts.new((R * c, R * s, z1)),
            bm.verts.new((INNER_R * c, INNER_R * s, z0)),
            bm.verts.new((INNER_R * c, INNER_R * s, z1)),
        ])

    for i in range(PLATE_ARC_SEGMENTS):
        a, b = i * 4, (i + 1) * 4
        bm.faces.new((verts[a],     verts[a + 1], verts[b + 1], verts[b]))      # внешняя
        bm.faces.new((verts[a + 2], verts[b + 2], verts[b + 3], verts[a + 3]))  # внутренняя
        bm.faces.new((verts[a],     verts[b],     verts[b + 2], verts[a + 2]))  # нижняя
        bm.faces.new((verts[a + 1], verts[a + 3], verts[b + 3], verts[b + 1]))  # верхняя

    last = PLATE_ARC_SEGMENTS * 4
    bm.faces.new((verts[0], verts[2], verts[3], verts[1]))                      # торец
    bm.faces.new((verts[last], verts[last + 1], verts[last + 3], verts[last + 2]))


def finish_plate_object(name, bm, material, collection):
    bmesh.ops.recalc_face_normals(bm, faces=bm.faces)

    mesh = bpy.data.meshes.new(name + " Mesh")
    bm.to_mesh(mesh)
    bm.free()

    for poly in mesh.polygons:
        poly.use_smooth = True
    if hasattr(mesh, "use_auto_smooth"):      # Blender < 4.1
        mesh.use_auto_smooth = True

    mesh.materials.append(material)

    obj = bpy.data.objects.new(name, mesh)
    collection.objects.link(obj)

    bevel = obj.modifiers.new("Soft Plate Edges", "BEVEL")
    bevel.width = ACTUAL_BEVEL
    bevel.segments = BEVEL_SEGMENTS
    bevel.limit_method = "ANGLE"
    bevel.angle_limit = math.radians(30)

    wn = obj.modifiers.new("Weighted Normals", "WEIGHTED_NORMAL")
    wn.keep_sharp = True

    return obj


def create_plates(material, collection):
    if SEPARATE_PLATES:
        for row in range(HORIZONTAL_ROWS):
            for col in range(VERTICAL_LINES):
                bm = bmesh.new()
                add_plate_geometry(bm, row, col)
                finish_plate_object(
                    f"Plate_R{row + 1:02d}_C{col + 1:02d}", bm, material, collection
                )
    else:
        # Все плиты в одном меше: в разы быстрее и легче для вьюпорта
        bm = bmesh.new()
        for row in range(HORIZONTAL_ROWS):
            for col in range(VERTICAL_LINES):
                add_plate_geometry(bm, row, col)
        finish_plate_object("Armor Plates", bm, material, collection)


# ============================================================
# 6. ТРУБКИ
# ============================================================

def create_curve(name, points, radius, material, collection, cyclic=False):
    curve = bpy.data.curves.new(name, "CURVE")
    curve.dimensions = "3D"
    curve.resolution_u = 2
    curve.bevel_depth = radius
    curve.bevel_resolution = 4
    curve.use_fill_caps = True            # закрытые торцы у вертикальных трубок

    spline = curve.splines.new("POLY")
    spline.points.add(len(points) - 1)
    for point, (x, y, z) in zip(spline.points, points):
        point.co = (x, y, z, 1)
    spline.use_cyclic_u = cyclic

    obj = bpy.data.objects.new(name, curve)
    collection.objects.link(obj)
    obj.data.materials.append(material)
    return obj


def create_tube_pair(name, points, glass_mat, glass_col, energy_col, vertical, cyclic=False):
    offset = FLOW_RNG.uniform(0.0, 2 * math.pi)     # своя фаза у каждой трубки

    create_curve(f"Glass_{name}", points, TUBE_RADIUS, glass_mat, glass_col, cyclic)

    energy = create_curve(
        f"Energy_{name}", points, ENERGY_CORE_R,
        ENERGY_MATS[vertical], energy_col, cyclic
    )
    energy["flow_offset"] = offset

    if ENERGY_HALO:
        halo = create_curve(
            f"Halo_{name}", points, ENERGY_HALO_RADIUS,
            HALO_MATS[vertical], energy_col, cyclic
        )
        halo["flow_offset"] = offset
        try:
            halo.visible_shadow = False             # ореол не отбрасывает тень
        except AttributeError:
            pass


def create_tubes(glass_mat, glass_col, energy_col):
    # Вертикальные
    for i in range(VERTICAL_LINES):
        ang = i * STEP_ANGLE
        x = TUBE_DISTANCE * math.cos(ang)
        y = TUBE_DISTANCE * math.sin(ang)
        points = [(x, y, -HEIGHT / 2), (x, y, HEIGHT / 2)]
        create_tube_pair(f"Vertical_{i + 1:02d}", points,
                         glass_mat, glass_col, energy_col, vertical=True)

    # Горизонтальные кольца
    ring_xy = [
        (TUBE_DISTANCE * math.cos(2 * math.pi * i / CIRCLE_SEGMENTS),
         TUBE_DISTANCE * math.sin(2 * math.pi * i / CIRCLE_SEGMENTS))
        for i in range(CIRCLE_SEGMENTS)
    ]
    for row in range(HORIZONTAL_ROWS + 1):
        z = -HEIGHT / 2 + row * ROW_HEIGHT
        points = [(x, y, z) for x, y in ring_xy]
        create_tube_pair(f"Horizontal_{row + 1:02d}", points,
                         glass_mat, glass_col, energy_col, vertical=False, cyclic=True)


# ============================================================
# 7. ЦЕНТРАЛЬНЫЙ КОРПУС
# ============================================================

def create_core(material, collection):
    bpy.ops.mesh.primitive_cylinder_add(
        vertices=96,
        radius=INNER_R - 0.05,
        depth=HEIGHT,
        location=(0, 0, 0),
    )
    body = bpy.context.active_object
    body.name = "Central Energy Housing"
    body.data.materials.append(material)
    for poly in body.data.polygons:
        poly.use_smooth = True
    move_to_collection(body, collection)


# ============================================================
# 8. СЦЕНА: МИР, КАМЕРА, СВЕТ
# ============================================================

def setup_scene():
    scene = bpy.context.scene

    world = bpy.data.worlds.new("Dark Sci-Fi World")
    world.use_nodes = True
    bg = world.node_tree.nodes.get("Background")
    if bg:
        bg.inputs["Color"].default_value = (0.004, 0.006, 0.012, 1.0)
        bg.inputs["Strength"].default_value = 1.0
    scene.world = world

    target = bpy.data.objects.new("Camera Target", None)
    target.location = (0, 0, 0)
    scene.collection.objects.link(target)

    cam_data = bpy.data.cameras.new("Column Camera")
    cam_data.lens = 35
    cam = bpy.data.objects.new("Column Camera", cam_data)
    cam.location = (55, -55, 15)
    scene.collection.objects.link(cam)

    track = cam.constraints.new("TRACK_TO")
    track.target = target
    track.track_axis = "TRACK_NEGATIVE_Z"
    track.up_axis = "UP_Y"
    scene.camera = cam

    light_data = bpy.data.lights.new("Key Light", "AREA")
    light_data.energy = 3000
    light_data.size = 15
    light_data.color = (0.6, 0.8, 1.0)
    light = bpy.data.objects.new("Key Light", light_data)
    light.location = (20, -14, 18)
    scene.collection.objects.link(light)
    track_l = light.constraints.new("TRACK_TO")
    track_l.target = target
    track_l.track_axis = "TRACK_NEGATIVE_Z"
    track_l.up_axis = "UP_Y"


def setup_glow_post():
    """Glare в композиторе: свечение зелёной энергии (Blender 3.x - 5.x)."""
    scene = bpy.context.scene

    # EEVEE Legacy (Blender < 4.2)
    eevee = scene.eevee
    if hasattr(eevee, "use_bloom"):
        eevee.use_bloom = True
        eevee.bloom_threshold = ENERGY_GLOW_THRESHOLD
        eevee.bloom_intensity = 0.2
        eevee.bloom_radius = 6.0

    try:
        if hasattr(scene, "compositing_node_group"):      # Blender 5.0+
            tree = bpy.data.node_groups.new("Energy Glow", "CompositorNodeTree")
            scene.compositing_node_group = tree
            tree.interface.new_socket("Image", in_out="OUTPUT", socket_type="NodeSocketColor")
            out_node = tree.nodes.new("NodeGroupOutput")
        else:                                              # Blender 3.x / 4.x
            scene.use_nodes = True
            tree = scene.node_tree
            tree.nodes.clear()
            out_node = tree.nodes.new("CompositorNodeComposite")

        render = tree.nodes.new("CompositorNodeRLayers")
        glare = tree.nodes.new("CompositorNodeGlare")

        render.location = (-300, 0)
        glare.location = (0, 0)
        out_node.location = (300, 0)

        try:
            glare.glare_type = "BLOOM"        # Blender 4.2+
        except TypeError:
            glare.glare_type = "FOG_GLOW"

        for attr, value in (("quality", "HIGH"), ("threshold", ENERGY_GLOW_THRESHOLD), ("size", 8)):
            if hasattr(glare, attr):
                try:
                    setattr(glare, attr, value)
                except (TypeError, AttributeError):
                    pass

        # В Blender 5.x параметры Glare — входные сокеты
        set_input(glare, ["Threshold"], ENERGY_GLOW_THRESHOLD)

        tree.links.new(render.outputs["Image"], glare.inputs["Image"])
        tree.links.new(glare.outputs["Image"], out_node.inputs[0])
    except Exception as err:
        print("Glare в композиторе настроить не удалось, добавьте узел Glare вручную:", err)


# ============================================================
# 9. ГЛАВНАЯ ФУНКЦИЯ
# ============================================================

def main():
    clear_scene()

    plates_col = make_collection("01 - Armor Plates")
    glass_col = make_collection("02 - Glass Tubes")
    energy_col = make_collection("03 - Energy")
    core_col = make_collection("04 - Inner Core")

    plate_mat = make_principled(
        "Graphite Blue - Armor", PLATE_COLOR, PLATE_ROUGHNESS, PLATE_METALLIC
    )
    glass_mat = make_glass_material()
    for vertical in (True, False):
        ENERGY_MATS[vertical] = make_energy_material(vertical)
        if ENERGY_HALO:
            HALO_MATS[vertical] = make_halo_material(vertical)
    core_mat = make_principled("Inner Body - Dark Blue", CORE_COLOR, 0.55, 0.15)

    create_plates(plate_mat, plates_col)
    create_tubes(glass_mat, glass_col, energy_col)

    if CREATE_INNER_BODY:
        create_core(core_mat, core_col)

    if SETUP_SCENE:
        setup_scene()

    if ENERGY_GLOW_POST:
        setup_glow_post()

    if EXPORT_FBX:
        export_fbx()

    bpy.ops.object.select_all(action="DESELECT")

    print("-----------------------------------")
    print("SCI-FI ENERGY COLUMN CREATED")
    print(f"Height: {HEIGHT}  Diameter: {DIAMETER}")
    print(f"Lines: {VERTICAL_LINES}  Rows: {HORIZONTAL_ROWS}")
    print(f"Plates as separate objects: {SEPARATE_PLATES}")
    print("Energy: бегущие капли, скорость течения зависит от номера кадра")
    print("Нажмите Space (Play), чтобы увидеть поток в Material/Rendered Preview")
    print("-----------------------------------")


# ============================================================
# 10. ЭКСПОРТ В UNITY (FBX)
# ============================================================
# Течение энергии — это шейдер; FBX несёт только геометрию и имена
# материалов. Bevel-геометрию кривых Blender FBX-экспортёр не понимает,
# поэтому трубки сначала конвертируются в обычные меши. Пивот каждого
# меша ставится в центр объёма: шейдер в Unity берёт фазу течения из
# позиции объекта — так у каждой трубки свой сдвиг.

def export_fbx():
    import os

    if bpy.context.object and bpy.context.object.mode != "OBJECT":
        bpy.ops.object.mode_set(mode="OBJECT")

    # 1. Кривые (стекло + энергия + ореол) -> обычные меши
    converted = [o for o in bpy.data.objects if o.type == "CURVE"]
    if converted:
        bpy.ops.object.select_all(action="DESELECT")
        for o in converted:
            o.select_set(True)
        bpy.context.view_layer.objects.active = converted[0]
        bpy.ops.object.convert(target="MESH")

        # 2. Пивот — в центр объёма меша (фаза течения в Unity берётся из позиции объекта)
        bpy.ops.object.select_all(action="DESELECT")
        for o in converted:
            o.select_set(True)
        bpy.context.view_layer.objects.active = converted[0]
        bpy.ops.object.origin_set(type="ORIGIN_CENTER_OF_VOLUME")

    # 3. FBX рядом с .blend (если файл сохранён), иначе — в домашнюю папку
    blend_dir = os.path.dirname(bpy.data.filepath)
    out_dir = blend_dir if blend_dir else os.path.expanduser("~")
    path = os.path.join(out_dir, FBX_FILENAME)

    bpy.ops.object.select_all(action="SELECT")
    bpy.ops.export_scene.fbx(
        filepath=path,
        use_selection=True,
        apply_unit_scale=True,
        apply_scale_options="FBX_SCALE_NONE",   # 1 юнит Unity = 1 метр
        use_mesh_modifiers=True,                 # применить Bevel / Weighted Normals плит
        mesh_smooth_type="OFF",                  # нормали берутся из геометрии
        object_types={"MESH"},
        add_leaf_bones=False,
    )
    print("FBX для Unity сохранён:", path)
    print("Имена материалов в FBX:")
    print("  Energy - Flow Vertical, Energy - Flow Ring, Transparent Glass - Tubes,")
    print("  Graphite Blue - Armor, Inner Body - Dark Blue")


main()
