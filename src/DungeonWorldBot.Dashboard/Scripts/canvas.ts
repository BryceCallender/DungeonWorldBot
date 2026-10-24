// The canvas page's board. Built into wwwroot/canvas.js by `npm run build` (dotnet build runs it too).
import Konva from 'konva';
import { connect, type CanvasItemType, type CanvasLive, type CanvasPerson, type ItemJson, type SavedItem } from './canvas-live';

// ---- The board ----------------------------------------------------------------------------------------------------

// Everything is drawn and saved in these units, whatever size the screen is, so everyone's drawings line up.
// The board goes on forever in every direction. The stage fills the box on the page and is a camera onto it: zooming
// scales it, panning moves it.
// Boards used to be this size, so older drawings sit in it; an empty board opens looking at it.
const HOME = { x: 0, y: 0, width: 1600, height: 1000 };

const container = document.getElementById('container') as HTMLDivElement;
const canvasId = container.dataset.canvasId!;

const stage = new Konva.Stage({ container, width: container.clientWidth, height: container.clientHeight });
const grid = new Konva.Layer({ listening: false });        // squares to line things up on, drawn as you look around
const pictures = new Konva.Layer();                        // maps and handouts, under everything drawn on them
const drawing = new Konva.Layer();                         // saved strokes and text
const tokens = new Konva.Layer();                          // pieces you can move
const handles = new Konva.Layer();                         // the box and corners around selected text
const others = new Konva.Layer({ listening: false });      // other people's cursors and half-drawn strokes
stage.add(grid, pictures, drawing, tokens, handles, others);

// The part of the board you can see, in board units
function visibleArea() {
    const scale = stage.scaleX();
    return { x: -stage.x() / scale, y: -stage.y() / scale, width: stage.width() / scale, height: stage.height() / scale };
}

// A square every 50 units, drawn only where you're looking, since there's no end to them. Gray at low opacity reads
// on both the light and dark board. Lines stay 1px however far you zoom, and zoomed far out only every fifth one is
// drawn, so the board doesn't turn into gray mush. Taking the thumbnail points it at the part being pictured instead.
const GRID_SIZE = 50;
const surface = getComputedStyle(container).getPropertyValue('--surface').trim() || '#ffffff';
let gridArea: { x: number; y: number; width: number; height: number } | null = null;
grid.add(new Konva.Shape({
    sceneFunc: context => {
        const area = gridArea ?? visibleArea();
        const scale = stage.scaleX();
        context.fillStyle = surface;
        context.fillRect(area.x, area.y, area.width, area.height);

        const step = GRID_SIZE * scale < 10 ? GRID_SIZE * 5 : GRID_SIZE;
        context.beginPath();
        for (let x = Math.ceil(area.x / step) * step; x <= area.x + area.width; x += step) {
            context.moveTo(x, area.y);
            context.lineTo(x, area.y + area.height);
        }
        for (let y = Math.ceil(area.y / step) * step; y <= area.y + area.height; y += step) {
            context.moveTo(area.x, y);
            context.lineTo(area.x + area.width, y);
        }
        context.strokeStyle = 'rgba(128,128,128,0.18)';
        context.lineWidth = 1 / scale;
        context.stroke();
    },
}));

// ---- Pan and zoom --------------------------------------------------------------------------------------------------

// Until you pan or zoom, the view keeps fitting what's drawn as the window resizes and the board loads
let viewMoved = false;

// 100% is one board unit to a pixel
const MIN_ZOOM = 0.05;
const MAX_ZOOM = 5;

function resize() {
    // A box with no size yet (hidden, or still laying out) would make the scale 0; wait until it has one
    if (!container.clientWidth || !container.clientHeight) return;
    stage.size({ width: container.clientWidth, height: container.clientHeight });
    if (!viewMoved) fitView();
}

// Everything drawn, in board units, or null when nothing is
function contentBounds() {
    let left = Infinity, top = Infinity, right = -Infinity, bottom = -Infinity;
    for (const layer of [pictures, drawing, tokens])
        for (const item of layer.getChildren()) {
            if (!item.visible()) continue;
            const r = item.getClientRect({ relativeTo: stage });   // in board units, whatever the zoom
            left = Math.min(left, r.x); top = Math.min(top, r.y);
            right = Math.max(right, r.x + r.width); bottom = Math.max(bottom, r.y + r.height);
        }
    return left === Infinity ? null : { x: left, y: top, width: right - left, height: bottom - top };
}

// `box` (in board units) grown to at least `least` wide and in `aspect`'s shape, around the same middle
function frameAround(box: { x: number; y: number; width: number; height: number }, padding: number, least: number, aspect: number) {
    let width = Math.max(box.width + padding * 2, least);
    let height = Math.max(box.height + padding * 2, width / aspect);
    width = Math.max(width, height * aspect);
    height = width / aspect;
    return { x: box.x + box.width / 2 - width / 2, y: box.y + box.height / 2 - height / 2, width, height };
}

// Everything that's drawn, or where boards start when nothing is. One scribble isn't blown up to fill the screen.
function fitView() {
    const content = contentBounds();
    const frame = content ? frameAround(content, 40, HOME.width / 2, stage.width() / stage.height()) : HOME;
    const scale = clampZoom(Math.min(stage.width() / frame.width, stage.height() / frame.height));
    stage.scale({ x: scale, y: scale });
    stage.position({
        x: stage.width() / 2 - (frame.x + frame.width / 2) * scale,
        y: stage.height() / 2 - (frame.y + frame.height / 2) * scale,
    });
    viewMoved = false;
}

const clampZoom = (scale: number) => Math.min(Math.max(scale, MIN_ZOOM), MAX_ZOOM);

// Zoom by `factor`, keeping the board point under `screen` (a spot in the box, in pixels) where it is
function zoomAt(screen: { x: number; y: number }, factor: number) {
    const old = stage.scaleX();
    const next = clampZoom(old * factor);
    const board = { x: (screen.x - stage.x()) / old, y: (screen.y - stage.y()) / old };
    stage.scale({ x: next, y: next });
    stage.position({ x: screen.x - board.x * next, y: screen.y - board.y * next });
    viewMoved = true;
}

// Puts a board point in the middle of the box, keeping the zoom
function lookAt(point: { x: number; y: number }) {
    stage.position({ x: stage.width() / 2 - point.x * stage.scaleX(), y: stage.height() / 2 - point.y * stage.scaleY() });
    viewMoved = true;
}

const zoomLevel = document.querySelector<HTMLElement>('[data-zoom="fit"]');
stage.on('scaleXChange', () => {
    if (zoomLevel) zoomLevel.textContent = `${Math.round(stage.scaleX() * 100)}%`;
});

// Trackpads: two fingers pan, pinching zooms (the browser reports a pinch as a wheel with ctrlKey).
// Mice: the wheel pans up and down, Shift+wheel sideways, Ctrl/Cmd+wheel zooms.
stage.on('wheel', e => {
    e.evt.preventDefault();
    if (e.evt.ctrlKey || e.evt.metaKey) {
        // Clamped so one notch of a mouse wheel is a step, not a leap
        const delta = Math.max(-50, Math.min(50, e.evt.deltaY));
        zoomAt(stage.getPointerPosition()!, Math.exp(-delta * 0.01));
    } else {
        const dx = e.evt.shiftKey && !e.evt.deltaX ? e.evt.deltaY : e.evt.deltaX;
        const dy = e.evt.shiftKey && !e.evt.deltaX ? 0 : e.evt.deltaY;
        stage.position({ x: stage.x() - dx, y: stage.y() - dy });
        viewMoved = true;
    }
});

// Dragging the board itself pans: with the Move tool on empty space, or with any tool while Space is held.
// A token under the pointer wins, since Konva drags the deepest draggable thing.
let spaceHeld = false;
function updatePanning() {
    stage.draggable(tool === 'move' || spaceHeld);
    container.classList.toggle('panning', spaceHeld);
}
stage.on('dragmove', e => { if (e.target === stage) viewMoved = true; });

new ResizeObserver(resize).observe(container);
resize();

// Where the pointer is, in board units rather than screen pixels, to a tenth (more is just bigger JSON)
function pointer() {
    const { x, y } = stage.getRelativePointerPosition() ?? { x: 0, y: 0 };
    return { x: Math.round(x * 10) / 10, y: Math.round(y * 10) / 10 };
}

// ---- Items: turning saved JSON into shapes and back ----------------------------------------------------------------

const layerFor = (type: CanvasItemType) => (type === 'token' ? tokens : type === 'image' ? pictures : drawing);

// The node an item is saved as: the shape itself, or the group a token's circle and label sit in
function itemOf(shape: Konva.Node): Konva.Node | null {
    let node: Konva.Node | null = shape;
    while (node && !(node.getParent() instanceof Konva.Layer)) node = node.getParent();
    return node && node.getLayer() !== others ? node : null;
}

// Things the saved JSON doesn't say but the page needs, like how easy a thin line is to hit with the eraser
function prepare(node: Konva.Node, type: CanvasItemType) {
    if (node instanceof Konva.Line) node.hitStrokeWidth(20);
    if (type === 'shape' && node instanceof Konva.Shape) node.hitStrokeWidth(20);
    if (type === 'token' || type === 'image' || type === 'shape' || node instanceof Konva.Text) node.draggable(tool === 'move');
    showPictures(node);
}

// ---- Pictures ------------------------------------------------------------------------------------------------------

// A loaded picture can't be saved as JSON, so items keep its URL in a `src` attribute and get the picture put back
// here. Each URL is fetched once, however many tokens use it.
const loading = new Map<string, Promise<HTMLImageElement>>();
function loadPicture(src: string) {
    let picture = loading.get(src);
    if (!picture) {
        picture = new Promise((resolve, reject) => {
            const img = new Image();
            img.onload = () => resolve(img);
            img.onerror = reject;
            img.src = src;
        });
        loading.set(src, picture);
    }
    return picture;
}

// The picture URL a node keeps, if it has one
const srcOf = (node: Konva.Node): string | undefined => node.getAttr('src');

// The node, and anything in it, with a picture to show: a board image, or the circle of an image token
function showPictures(node: Konva.Node) {
    const all: Konva.Node[] = node instanceof Konva.Container ? [node, ...node.find((n: Konva.Node) => !!srcOf(n))] : [node];
    for (const target of all) {
        const src = srcOf(target);
        if (!src) continue;
        loadPicture(src).then(img => {
            if (target instanceof Konva.Image) {
                target.image(img);
            } else if (target instanceof Konva.Circle) {
                // Fill the circle with the middle of the picture, however wide or tall it is
                const scale = (target.radius() * 2) / Math.min(img.width, img.height);
                target.fillPatternImage(img);
                target.fillPriority('pattern');            // a shape with a color and a pattern shows the color otherwise
                target.fillPatternRepeat('no-repeat');
                target.fillPatternScale({ x: scale, y: scale });
                target.fillPatternOffset({ x: img.width / 2, y: img.height / 2 });
            }
        }).catch(() => { /* a picture that's gone leaves the shape plain */ });
    }
}

// Tokens the fight put on the board for its enemies (made by EnemyTokens on the server, which signs them with this).
// Only the DM can remove them, so a player's stray click can't take an enemy away; the server refuses it too.
const FIGHT_AUTHOR = 'encounter';
const runsFights = container.dataset.runsFights === 'true';
const locked = new Set<string>();
const isLocked = (node: Konva.Node) => !runsFights && locked.has(node.id());

function addItem(id: string, type: CanvasItemType, json: ItemJson, author?: string) {
    if (author === FIGHT_AUTHOR) locked.add(id);
    const wasSelected = selected()?.id() === id;
    stage.findOne('#' + id)?.destroy();                    // the same ID again replaces it
    let node = Konva.Node.create(json);
    if (type === 'token') node = withInitials(node);
    node.id(id);
    prepare(node, type);
    layerFor(type).add(node as Konva.Shape | Konva.Group);
    if (wasSelected) select(node);
    if (type === 'token') listTokens();
    return node;
}

function removeItem(node: Konva.Node) {
    locked.delete(node.id());
    if (selected() === node) select(null);
    const wasToken = node.getLayer() === tokens;
    node.destroy();
    if (wasToken) listTokens();
}

function load(items: SavedItem[]) {
    drawing.destroyChildren();
    tokens.destroyChildren();
    locked.clear();
    for (const item of items) addItem(item.id, item.type, item.item, item.authorID);
    listTokens();
    mine.length = 0;
}

// ---- The token list beside the board -------------------------------------------------------------------------------

const tokenList = document.querySelector<HTMLOListElement>('[data-tokens]')!;

// A token is a circle (like the sample ones) or a group with a circle and its initials in it
function tokenColor(token: Konva.Node) {
    const circle = token instanceof Konva.Circle ? token : (token as Konva.Group).findOne<Konva.Circle>('Circle');
    return (circle?.fill() as string | undefined) ?? '#888780';
}

function listTokens() {
    const all = tokens.getChildren();
    document.querySelector<HTMLElement>('[data-tokens-empty]')!.hidden = all.length > 0;
    tokenList.replaceChildren(...all.map(token => {
        const button = document.createElement('button');
        button.type = 'button';
        button.dataset.token = token.id();
        const dot = Object.assign(document.createElement('span'), { className: 'dot' });
        dot.style.background = tokenColor(token);
        const holder = token instanceof Konva.Group ? token.findOne((n: Konva.Node) => !!srcOf(n)) : token;
        const picture = holder && srcOf(holder);
        if (picture) dot.style.background = `center / cover url("${picture}")`;
        button.append(dot, Object.assign(document.createElement('span'), { className: 'name', textContent: token.name() || 'Token' }));
        const li = document.createElement('li');
        li.append(button);
        // An enemy's token stays until its fight is done with it
        if (isLocked(token)) return li;
        // Beside the name rather than in it, since a button can't hold another button
        const remove = Object.assign(document.createElement('button'), {
            type: 'button', className: 'remove', textContent: '×', title: `Remove ${token.name() || 'token'}`,
        });
        remove.dataset.removeToken = token.id();
        remove.setAttribute('aria-label', remove.title);
        li.append(remove);
        return li;
    }));
}

// Clicking a token in the list makes it jump, so you can find it on a busy board
// and its × takes it off the board for everyone
tokenList.addEventListener('click', e => {
    const removeId = (e.target as HTMLElement).closest<HTMLElement>('[data-remove-token]')?.dataset.removeToken;
    const removing = removeId && stage.findOne('#' + removeId);
    if (removing && !isLocked(removing)) {
        live?.removeItem(removing.id());
        removeItem(removing);
        return;
    }

    const id = (e.target as HTMLElement).closest<HTMLElement>('[data-token]')?.dataset.token;
    const token = id && stage.findOne('#' + id);
    if (!token) return;
    // From whatever size it's been made, and back to it
    const size = token.scaleX();
    token.to({ scaleX: size * 1.6, scaleY: size * 1.6, duration: 0.15, onFinish: () => token.to({ scaleX: size, scaleY: size, duration: 0.15 }) });
});

// ---- Tools ---------------------------------------------------------------------------------------------------------

type Tool = 'pen' | 'erase' | 'move' | 'token' | 'text' | 'shape';
type ShapeKind = 'rect' | 'ellipse' | 'line' | 'arrow';
let shapeKind: ShapeKind = 'rect';
let tool: Tool = 'move';
let color = (document.querySelector<HTMLElement>('[data-color].on')?.dataset.color) ?? '#7F77DD';
const widthSlider = document.querySelector<HTMLInputElement>('[data-width]')!;
let penWidth = Number(widthSlider.value);

// The strip of shape kinds beside the Shape button, which opens like a menu and closes once you've picked
const shapeTool = document.querySelector<HTMLElement>('.shape-tool')!;
const showShapes = (open: boolean) => shapeTool.classList.toggle('open', open);

function setTool(next: Tool) {
    if (next !== 'shape') showShapes(false);
    tool = next;
    container.dataset.tool = next;                         // for the cursor, in CanvasPage.razor.css
    document.querySelectorAll<HTMLElement>('.board-rail [data-tool]').forEach(b => b.classList.toggle('on', b.dataset.tool === next));
    tokens.getChildren().forEach(t => t.draggable(next === 'move'));
    drawing.find('Text, .shape').forEach(t => t.draggable(next === 'move'));
    pictures.getChildren().forEach(p => p.draggable(next === 'move'));
    if (next !== 'move') select(null);
    updatePanning();
}

function pickColor(next: string, swatch: Element) {
    color = next;
    document.querySelectorAll('.swatch, .color-picker').forEach(b => b.classList.toggle('on', b === swatch));
    // Picking a color means you want to draw with it, unless you're placing tokens, text or shapes in it
    if (tool !== 'token' && tool !== 'text' && tool !== 'shape') setTool('pen');
}

// The thickness slider, and the dot above it that shows how thick
widthSlider.addEventListener('input', () => {
    penWidth = Number(widthSlider.value);
    document.querySelector<HTMLElement>('[data-width-dot]')!.style.setProperty('--dot', penWidth + 'px');
    // Like a color: you want to draw with it, unless you're about to drag out a shape
    if (tool !== 'shape') setTool('pen');
});

// The palette button: the browser's own color picker, which fires as you drag around in it
const customColor = document.querySelector<HTMLInputElement>('[data-custom-color]')!;
customColor.addEventListener('input', () => {
    const swatch = customColor.closest<HTMLElement>('.color-picker')!;
    swatch.style.setProperty('--swatch', customColor.value);
    pickColor(customColor.value, swatch);
});
// Clicking it again once it has a color uses that color, as well as opening the picker
customColor.addEventListener('click', () => {
    const swatch = customColor.closest<HTMLElement>('.color-picker')!;
    if (swatch.style.getPropertyValue('--swatch')) pickColor(customColor.value, swatch);
});

// The tool rail, and the sidebar's "Add token"
document.querySelector('.board-page')!.addEventListener('click', e => {
    const button = (e.target as HTMLElement).closest('button');
    if (!button) return;
    // The Shape button opens its strip; clicking it again while it's open closes it
    if (button.dataset.tool === 'shape') showShapes(!(tool === 'shape' && shapeTool.classList.contains('open')));
    if (button.dataset.tool) setTool(button.dataset.tool as Tool);
    if (button.dataset.color) pickColor(button.dataset.color, button);
    if (button.hasAttribute('data-undo')) undo();
    if (button.dataset.shape) {
        shapeKind = button.dataset.shape as ShapeKind;
        document.querySelectorAll('[data-shape]').forEach(b => b.classList.toggle('on', b === button));
        setTool('shape');
        showShapes(false);
    }
    // The buttons zoom around the middle of the box
    const middle = { x: stage.width() / 2, y: stage.height() / 2 };
    if (button.dataset.zoom === 'in') zoomAt(middle, 1.25);
    if (button.dataset.zoom === 'out') zoomAt(middle, 0.8);
    if (button.dataset.zoom === 'fit') fitView();
});

const keys: Record<string, Tool> = { p: 'pen', e: 'erase', m: 'move', t: 'token', x: 'text', s: 'shape' };
document.addEventListener('keydown', e => {
    // Typing text on the board, or anywhere else, isn't a shortcut; nor is anything in an open dialog
    if ((e.target as HTMLElement).closest('input, textarea, select, [contenteditable], dialog')) return;
    if ((e.ctrlKey || e.metaKey) && e.key === 'z') { e.preventDefault(); undo(); return; }
    if ((e.ctrlKey || e.metaKey) && (e.key === '=' || e.key === '+')) { e.preventDefault(); zoomAt({ x: stage.width() / 2, y: stage.height() / 2 }, 1.25); return; }
    if ((e.ctrlKey || e.metaKey) && e.key === '-') { e.preventDefault(); zoomAt({ x: stage.width() / 2, y: stage.height() / 2 }, 0.8); return; }
    if ((e.ctrlKey || e.metaKey) && e.key === '0') { e.preventDefault(); fitView(); return; }
    if (e.key === ' ' && !spaceHeld) { e.preventDefault(); spaceHeld = true; updatePanning(); return; }
    const chosen = selected();
    if (chosen && (e.key === 'Delete' || e.key === 'Backspace')) { if (!isLocked(chosen)) { live?.removeItem(chosen.id()); removeItem(chosen); } return; }
    if (!e.ctrlKey && !e.metaKey && !e.altKey && keys[e.key]) setTool(keys[e.key]);
});
document.addEventListener('keyup', e => {
    if (e.key === ' ') { spaceHeld = false; updatePanning(); }
});

// Like a menu: pressing anywhere else (the board included) or Escape closes the shape strip
document.addEventListener('pointerdown', e => {
    if (!(e.target as HTMLElement).closest('.shape-tool')) showShapes(false);
});
document.addEventListener('keydown', e => { if (e.key === 'Escape') showShapes(false); });

// What you've added, newest last, for undo
const mine: string[] = [];

function undo() {
    while (mine.length) {
        const node = stage.findOne('#' + mine.pop()!);
        if (!node) continue;                               // someone already erased it
        live?.removeItem(node.id());
        removeItem(node);
        return;
    }
}

// ---- Drawing -------------------------------------------------------------------------------------------------------

// The stroke being drawn, and the points not sent to the others yet
let stroke: { line: Konva.Line; unsent: number[]; timer: number | null } | null = null;
let erasing = false;

stage.on('pointerdown', e => {
    if (spaceHeld) return;                                 // the stage drag pans instead
    const { x, y } = pointer();
    if (tool === 'pen') {
        const line = new Konva.Line({
            id: crypto.randomUUID(),
            points: [x, y],
            stroke: color,
            strokeWidth: penWidth,
            lineCap: 'round',
            lineJoin: 'round',
            tension: 0.5,
            hitStrokeWidth: 20,
        });
        drawing.add(line);
        stroke = { line, unsent: [x, y], timer: null };
    } else if (tool === 'move') {
        // Text, shapes, pictures and tokens under the pointer get selection handles; anywhere else drops them.
        // Handles themselves are left alone.
        if (e.target.getParent() instanceof Konva.Transformer) return;
        const onDrawing = e.target.getLayer() === drawing;
        const text = e.target instanceof Konva.Text && onDrawing;
        const shape = e.target.hasName('shape') && onDrawing;
        const picture = e.target instanceof Konva.Image && e.target.getLayer() === pictures;
        // A token is a group, so whichever part was clicked (its circle or initials), the whole token is selected
        const token = e.target.getLayer() === tokens ? itemOf(e.target) : null;
        select(token ?? (text || shape || picture ? e.target : null));
    } else if (tool === 'erase') {
        erasing = true;
        eraseHere();
    } else if (tool === 'token') {
        placeToken(x, y);
    } else if (tool === 'shape') {
        shapeDraft = { node: newShape(shapeKind, x, y), start: { x, y } };
        drawing.add(shapeDraft.node);
    } else if (tool === 'text') {
        // Clicking existing text edits it; clicking anywhere else starts new text there
        const target = e.target instanceof Konva.Text && e.target.getLayer() === drawing ? e.target : null;
        editText(target, target ? target.position() : { x, y });
    }
});

stage.on('pointermove', () => {
    const { x, y } = pointer();
    live?.cursor(x, y);

    if (stroke) {
        const points = stroke.line.points();
        // Skip points too close to the last one; they add size, not shape
        if (Math.hypot(x - points[points.length - 2], y - points[points.length - 1]) < 2) return;
        stroke.line.points(points.concat(x, y));
        stroke.unsent.push(x, y);
        // Send the new points in batches, about 20 a second, so the others see it being drawn
        stroke.timer ??= window.setTimeout(sendProgress, 50);
    } else if (shapeDraft) {
        sizeShape(shapeDraft.node, shapeDraft.start, { x, y }, shiftHeld);
    } else if (erasing) {
        eraseHere();
    }
});

function sendProgress() {
    if (!stroke) return;
    stroke.timer = null;
    if (stroke.unsent.length) live?.strokeProgress(stroke.line.id(), stroke.unsent.splice(0));
}

// On the window, so letting go outside the board still finishes the stroke
window.addEventListener('pointerup', () => {
    erasing = false;
    if (shapeDraft) finishShape();
    if (!stroke) return;
    if (stroke.timer) clearTimeout(stroke.timer);
    sendProgress();
    const { line } = stroke;
    stroke = null;
    live?.strokeEnd(line.id(), line.toObject());
    mine.push(line.id());
});

function eraseHere() {
    const shape = stage.getIntersection(stage.getPointerPosition()!);
    const item = shape && itemOf(shape);
    // Sweeping the eraser over a map shouldn't take the whole map; pictures go with Move, then Delete
    if (!item || item.getLayer() === pictures || isLocked(item)) return;
    live?.removeItem(item.id());
    removeItem(item);
}

// ---- Shapes --------------------------------------------------------------------------------------------------------

// The shape being dragged out, from where it started
let shapeDraft: { node: Konva.Shape; start: { x: number; y: number } } | null = null;

// Shift makes squares, circles and lines at 45° steps
let shiftHeld = false;
document.addEventListener('keydown', e => { if (e.key === 'Shift') shiftHeld = true; });
document.addEventListener('keyup', e => { if (e.key === 'Shift') shiftHeld = false; });

function newShape(kind: ShapeKind, x: number, y: number): Konva.Shape {
    // Named 'shape' so Move can tell them from freehand strokes. Lines keep their width however they're resized.
    const look = {
        id: crypto.randomUUID(), name: 'shape', x, y, stroke: color, strokeWidth: penWidth,
        strokeScaleEnabled: false, hitStrokeWidth: 20, lineCap: 'round' as const, lineJoin: 'round' as const,
    };
    switch (kind) {
        case 'rect': return new Konva.Rect({ ...look, width: 0, height: 0, cornerRadius: 4 });
        case 'ellipse': return new Konva.Ellipse({ ...look, radiusX: 0, radiusY: 0 });
        case 'line': return new Konva.Line({ ...look, points: [0, 0, 0, 0] });
        case 'arrow': return new Konva.Arrow({ ...look, points: [0, 0, 0, 0], fill: color, pointerLength: Math.max(14, penWidth * 3), pointerWidth: Math.max(14, penWidth * 3) });
    }
}

// Stretch it from where the drag started to where the pointer is
function sizeShape(node: Konva.Shape, start: { x: number; y: number }, at: { x: number; y: number }, even: boolean) {
    let dx = at.x - start.x, dy = at.y - start.y;
    if (node instanceof Konva.Line) {                     // a line or arrow, drawn from its start
        if (even) {
            const angle = Math.round(Math.atan2(dy, dx) / (Math.PI / 4)) * (Math.PI / 4);
            const length = Math.hypot(dx, dy);
            dx = Math.round(Math.cos(angle) * length);
            dy = Math.round(Math.sin(angle) * length);
        }
        node.points([0, 0, dx, dy]);
        return;
    }
    if (even) {
        const side = Math.max(Math.abs(dx), Math.abs(dy));
        dx = Math.sign(dx || 1) * side;
        dy = Math.sign(dy || 1) * side;
    }
    if (node instanceof Konva.Ellipse) {                  // placed by its middle
        node.position({ x: start.x + dx / 2, y: start.y + dy / 2 });
        node.radius({ x: Math.abs(dx) / 2, y: Math.abs(dy) / 2 });
    } else {                                              // a rectangle, placed by its top-left corner
        node.position({ x: Math.min(start.x, start.x + dx), y: Math.min(start.y, start.y + dy) });
        node.size({ width: Math.abs(dx), height: Math.abs(dy) });
    }
}

function finishShape() {
    const { node } = shapeDraft!;
    shapeDraft = null;
    // A click without a drag isn't a shape
    const box = node.getClientRect({ skipStroke: true, relativeTo: stage });
    if (Math.max(box.width, box.height) < 4) { node.destroy(); return; }
    live?.addItem(node.id(), 'shape', node.toObject());
    mine.push(node.id());
}

// ---- Moving and scaling text, shapes and pictures --------------------------------------------------------------------------------------

// Konva's Transformer draws a box with corner handles around whatever it's given. Dragging a corner scales the text,
// picture or token; dragging it moves it (they're draggable with the Move tool).
const transformer = new Konva.Transformer({
    keepRatio: true,
    rotateEnabled: false,
    enabledAnchors: ['top-left', 'top-right', 'bottom-left', 'bottom-right'],
    anchorSize: 9,
    anchorCornerRadius: 2,
    borderStroke: '#7F77DD',
    anchorStroke: '#7F77DD',
    // Not smaller than readable
    boundBoxFunc: (old, next) => (next.height < 10 ? old : next),
});
handles.add(transformer);

const selected = () => transformer.nodes()[0] ?? null;
const isToken = (node: Konva.Node) => node.getLayer() === tokens;
function select(shape: Konva.Node | null) {
    // Text, pictures and tokens keep their shape; drawn shapes can be stretched either way. (A token's name is what
    // it's called, which could be anything, even "shape".)
    const free = !!shape && !isToken(shape) && shape.hasName('shape');
    transformer.keepRatio(!free);
    transformer.enabledAnchors(free
        ? ['top-left', 'top-center', 'top-right', 'middle-left', 'middle-right', 'bottom-left', 'bottom-center', 'bottom-right']
        : ['top-left', 'top-right', 'bottom-left', 'bottom-right']);
    transformer.nodes(shape ? [shape] : []);
}

// The handles scale it; when you let go, the scale becomes a real font size or width and height, so text stays crisp
// and what's saved is plain bigger (or smaller) text or picture
transformer.on('transformend', () => {
    const shape = selected();
    if (!shape) return;
    if (isToken(shape)) {
        // Tokens keep their scale too: a token is a circle with initials or a picture in it, all of which grow with it
        shape.scale({ x: Math.round(shape.scaleX() * 100) / 100, y: Math.round(shape.scaleX() * 100) / 100 });
        shape.x(Math.round(shape.x() * 10) / 10);
        shape.y(Math.round(shape.y() * 10) / 10);
        live?.addItem(shape.id(), 'token', shape.toObject());
        return;
    }
    if (shape.hasName('shape')) {
        // Shapes keep their scale: their lines don't thicken (strokeScaleEnabled is off), so there's nothing to convert
        shape.x(Math.round(shape.x() * 10) / 10);
        shape.y(Math.round(shape.y() * 10) / 10);
        live?.addItem(shape.id(), 'shape', shape.toObject());
        return;
    }
    if (shape instanceof Konva.Text) {
        shape.fontSize(Math.max(8, Math.round(shape.fontSize() * shape.scaleX())));
    } else {
        shape.width(Math.round(shape.width() * shape.scaleX()));
        shape.height(Math.round(shape.height() * shape.scaleY()));
    }
    shape.scale({ x: 1, y: 1 });
    shape.x(Math.round(shape.x() * 10) / 10);
    shape.y(Math.round(shape.y() * 10) / 10);
    live?.addItem(shape.id(), shape instanceof Konva.Text ? 'text' : 'image', shape.toObject());
});

// Double-click text with the Move tool to change the words
drawing.on('dblclick dbltap', e => {
    if (tool === 'move' && e.target instanceof Konva.Text) {
        select(null);
        editText(e.target, e.target.position());
    }
});

// ---- Text ----------------------------------------------------------------------------------------------------------

const TEXT_SIZE = 22;

// Typing happens in a real <textarea> laid over the board at the right spot and zoom, since a canvas can't take
// typing itself. Enter (or clicking away) puts it on the board as a Konva.Text; Escape cancels; Shift+Enter adds a line.
function editText(existing: Konva.Text | null, at: { x: number; y: number }) {
    // Where that board point is on screen, after zoom and pan
    const screen = stage.getAbsoluteTransform().point(at);
    const scale = stage.scaleX();

    const editor = document.createElement('textarea');
    editor.className = 'board-text-editor';
    editor.rows = 1;
    editor.value = existing?.text() ?? '';
    Object.assign(editor.style, {
        left: `${screen.x}px`,
        top: `${screen.y}px`,
        fontSize: `${(existing?.fontSize() ?? TEXT_SIZE) * scale}px`,
        color: (existing?.fill() as string | undefined) ?? color,
    });
    existing?.hide();                                      // so the old words don't show through while editing
    container.append(editor);
    // After this click has finished, or the board takes the focus straight back
    setTimeout(() => editor.focus());

    let finished = false;
    function finish(save: boolean) {
        if (finished) return;
        finished = true;
        const words = editor.value.trim();
        editor.remove();

        if (existing) {
            existing.show();
            if (!save || words === existing.text()) return;
            if (!words) {                                  // emptied out: it goes
                live?.removeItem(existing.id());
                removeItem(existing);
                return;
            }
            existing.text(words);
            live?.addItem(existing.id(), 'text', existing.toObject());   // the same ID replaces it for everyone
            return;
        }

        if (!save || !words) return;
        const text = new Konva.Text({
            id: crypto.randomUUID(), x: at.x, y: at.y, text: words,
            fontSize: TEXT_SIZE, fontFamily: 'system-ui, sans-serif', fill: color,
        });
        drawing.add(text);
        live?.addItem(text.id(), 'text', text.toObject());
        mine.push(text.id());
    }

    editor.addEventListener('keydown', e => {
        if (e.key === 'Enter' && !e.shiftKey) { e.preventDefault(); finish(true); }
        if (e.key === 'Escape') finish(false);
    });
    editor.addEventListener('blur', () => finish(true));
}

// ---- Tokens --------------------------------------------------------------------------------------------------------

// "Goblin boss" is GB, "Kael" is K: the first letter of up to two words
const initialsOf = (name: string) =>
    name.split(/\s+/).filter(Boolean).slice(0, 2).map(word => [...word][0]).join('').toUpperCase();

// Their initials in white, centred in a token's circle and sized to it
function addInitials(token: Konva.Group, name: string, radius: number) {
    token.add(new Konva.Text({
        text: initialsOf(name), fontSize: Math.round(radius * 0.8), fontStyle: 'bold', fill: '#ffffff',
        width: radius * 2, height: radius * 2, offsetX: radius, offsetY: radius,
        align: 'center', verticalAlign: 'middle', listening: false,
    }));
}

// Tokens without a picture show initials, including ones saved as a bare circle (like the samples) or before tokens
// had them. The circle is wrapped in a group just for showing; it sits at the same spot, so moves still line up.
function withInitials(token: Konva.Node): Konva.Node {
    if (srcOf(token)) return token;
    if (token instanceof Konva.Circle) {
        const group = new Konva.Group({ x: token.x(), y: token.y(), name: token.name() });
        token.position({ x: 0, y: 0 });
        token.name('');
        // Old saves have the circle draggable, and Konva drags the innermost draggable thing, which would leave the
        // group (the saved item) where it was
        token.draggable(false);
        group.add(token);
        addInitials(group, group.name(), token.radius());
        return group;
    }
    if (token instanceof Konva.Group) {
        const circle = token.findOne<Konva.Circle>('Circle');
        if (circle && !srcOf(circle) && !token.findOne('Text')) addInitials(token, token.name(), circle.radius());
    }
    return token;
}

// New tokens start big enough to read their initials at a glance (a grid square and a half across); the corner
// handles shrink them for a crowded board
const TOKEN_RADIUS = 36;

// A colored circle with their initials, or with a picture in it if there's one
function placeToken(x: number, y: number, picture?: string, name = window.prompt('Token name')?.trim()) {
    if (!name) return;

    const token = new Konva.Group({ id: crypto.randomUUID(), x, y, name });
    const circle = new Konva.Circle({ radius: TOKEN_RADIUS, fill: color, stroke: '#ffffff', strokeWidth: 2 });
    if (picture) circle.setAttr('src', picture);
    token.add(circle);
    if (!picture) addInitials(token, name, circle.radius());
    prepare(token, 'token');
    tokens.add(token);
    listTokens();
    live?.addItem(token.id(), 'token', token.toObject());
    mine.push(token.id());
}

// Others see a dragged token or text move about 12 times a second; where it's dropped is always sent
let lastMove = 0;
for (const layer of [tokens, drawing, pictures]) {
    layer.on('dragmove', e => {
        // Dragging takes over the pointer, so the stage's pointermove doesn't send the cursor meanwhile
        const { x, y } = pointer();
        live?.cursor(x, y);
        if (Date.now() - lastMove < 80) return;
        lastMove = Date.now();
        live?.moveItem(e.target.id(), e.target.x(), e.target.y());
    });
    layer.on('dragend', e => live?.moveItem(e.target.id(), e.target.x(), e.target.y()));
}

// ---- Adding pictures -----------------------------------------------------------------------------------------------

// Pictures are shrunk in the browser before uploading: plenty sharp on the board, and small to store and send.
// The server only checks the file really is a picture; it never has to decode one.
const TOKEN_PICTURE_SIZE = 512;
const BOARD_PICTURE_SIZE = 2560;

async function shrink(file: File, largest: number) {
    const bitmap = await createImageBitmap(file);            // fails for anything that isn't a picture
    const scale = Math.min(1, largest / Math.max(bitmap.width, bitmap.height));
    const canvas = new OffscreenCanvas(Math.round(bitmap.width * scale), Math.round(bitmap.height * scale));
    canvas.getContext('2d')!.drawImage(bitmap, 0, 0, canvas.width, canvas.height);
    bitmap.close();
    // WebP where the browser can make it (Safari can't, and gives PNG instead)
    return { blob: await canvas.convertToBlob({ type: 'image/webp', quality: 0.85 }), width: canvas.width, height: canvas.height };
}

// The form's antiforgery token, from <AntiforgeryToken /> on the page, which the upload has to send back
const antiforgery = () => document.querySelector<HTMLInputElement>('input[name="__RequestVerificationToken"]')?.value ?? '';

async function upload(file: File, largest: number) {
    const { blob, width, height } = await shrink(file, largest);
    const form = new FormData();
    form.append('file', blob, 'picture');
    form.append('__RequestVerificationToken', antiforgery());
    const response = await fetch(`api/canvases/${canvasId}/images`, { method: 'POST', body: form });
    if (!response.ok) throw new Error((await response.text()) || `Upload failed (${response.status})`);
    const { url } = await response.json() as { url: string };
    return { url, width, height };
}

// The board point in the middle of what you can see
const viewCenter = () => stage.getAbsoluteTransform().copy().invert().point({ x: stage.width() / 2, y: stage.height() / 2 });

// A map or handout on the board, centred where you're looking (or where it was dropped), at most 800 by 600 units
async function addPicture(file: File, at = viewCenter()) {
    const { url, width, height } = await upload(file, BOARD_PICTURE_SIZE);
    const scale = Math.min(1, 800 / width, 600 / height);
    const picture = new Konva.Image({
        id: crypto.randomUUID(), image: undefined,
        x: Math.round(at.x - (width * scale) / 2), y: Math.round(at.y - (height * scale) / 2),
        width: Math.round(width * scale), height: Math.round(height * scale),
    });
    picture.setAttr('src', url);
    prepare(picture, 'image');
    pictures.add(picture);
    live?.addItem(picture.id(), 'image', picture.toObject());
    mine.push(picture.id());
}

async function addPictureToken(file: File, at = viewCenter()) {
    const name = window.prompt('Token name')?.trim();
    if (!name) return;
    const { url } = await upload(file, TOKEN_PICTURE_SIZE);
    placeToken(at.x, at.y, url, name);
}

// A picture for the enemies being added from the fight's dialog goes up the same way as an image token's. The form
// then carries its address, and the server puts it on each enemy's token.
document.querySelector<HTMLFormElement>('#add-enemies form')?.addEventListener('submit', async e => {
    const form = e.currentTarget as HTMLFormElement;
    const file = form.querySelector<HTMLInputElement>('[data-enemy-picture]')?.files?.[0];
    if (!file) return;

    e.preventDefault();
    const add = form.querySelector<HTMLButtonElement>('.create');
    if (add) { add.disabled = true; add.textContent = 'Uploading…'; }
    try {
        const { url } = await upload(file, TOKEN_PICTURE_SIZE);
        form.querySelector<HTMLInputElement>('input[name="pictureUrl"]')!.value = url;
        form.submit();                                     // doesn't fire submit again
    } catch (err) {
        window.alert(`Couldn't add that picture: ${err instanceof Error ? err.message : err}`);
        if (add) { add.disabled = false; add.textContent = 'Add'; }
    }
});

// A picture on the board, or a token's picture (only from the sidebar's "+ Image token")
function addFile(file: File | undefined, asToken: boolean, at?: { x: number; y: number }) {
    if (!file?.type.startsWith('image/')) return;
    (asToken ? addPictureToken(file, at) : addPicture(file, at))
        .catch(err => window.alert(`Couldn't add that picture: ${err instanceof Error ? err.message : err}`));
}

// The rail's picture button and the sidebar's "+ Image token" share one hidden file picker
const filePicker = document.querySelector<HTMLInputElement>('[data-file]')!;
let pickingToken = false;
document.querySelector('.board-page')!.addEventListener('click', e => {
    const button = (e.target as HTMLElement).closest<HTMLElement>('[data-pick]');
    if (!button) return;
    pickingToken = button.dataset.pick === 'token';
    filePicker.value = '';
    filePicker.click();
});
filePicker.addEventListener('change', () => addFile(filePicker.files?.[0], pickingToken));

// Dropping a picture on the board puts it where it was dropped
container.addEventListener('dragover', e => e.preventDefault());
container.addEventListener('drop', e => {
    e.preventDefault();
    stage.setPointersPositions(e);
    addFile(e.dataTransfer?.files[0], false, stage.getRelativePointerPosition() ?? undefined);
});

// Pasting a picture (like a screenshot) puts it in the middle of what you can see
document.addEventListener('paste', e => {
    if ((e.target as HTMLElement).closest('input, textarea, select, [contenteditable], dialog')) return;
    const file = [...e.clipboardData?.files ?? []].find(f => f.type.startsWith('image/'));
    if (file) { e.preventDefault(); addFile(file, false); }
});

// ---- Thumbnail for the canvases page --------------------------------------------------------------------------

// A couple of seconds after your last change, the board takes a small picture of itself for its card on the canvases
// page: framed on what's drawn (see thumbnailFrame), wherever you're zoomed or panned to, without cursors or handles.
const THUMBNAIL_WIDTH = 480;
let thumbnailTimer = 0;

function thumbnailSoon() {
    clearTimeout(thumbnailTimer);
    thumbnailTimer = window.setTimeout(() => saveThumbnail().catch(() => { /* only the card misses out */ }), 2000);
}

// The part of the board worth showing, in board units: everything on it, with room around it, in the card's shape
// (16:10), never smaller than a third of where boards start so one scribble isn't blown up. An empty board shows
// where boards start.
function thumbnailFrame() {
    const content = contentBounds();
    return content ? frameAround(content, 40, HOME.width / 3, HOME.width / HOME.height) : HOME;
}

async function saveThumbnail() {
    const scale = stage.scaleX();
    const frame = thumbnailFrame();
    handles.hide();
    others.hide();
    try {
        // The grid is only drawn where you're looking; draw it under the frame instead, just while the picture is
        // drawn (which happens before toBlob returns), so the board on screen never shows it
        gridArea = frame;
        // Where that frame is on screen right now; Konva draws all of it, even parts panned out of view
        const picture = stage.toBlob({
            x: stage.x() + frame.x * scale, y: stage.y() + frame.y * scale,
            width: frame.width * scale, height: frame.height * scale,
            pixelRatio: THUMBNAIL_WIDTH / (frame.width * scale), mimeType: 'image/webp', quality: 0.8,
        });
        gridArea = null;
        const blob = await picture as Blob;
        const form = new FormData();
        form.append('file', blob, 'thumbnail');
        form.append('__RequestVerificationToken', antiforgery());
        await fetch(`api/canvases/${canvasId}/thumbnail`, { method: 'POST', body: form });
    } finally {
        gridArea = null;
        handles.show();
        others.show();
    }
}

// ---- Other people --------------------------------------------------------------------------------------------------

const people = new Map<string, CanvasPerson>();
const cursors = new Map<string, { group: Konva.Group; hide: number }>();
const drafts = new Map<string, Konva.Line>();               // their half-drawn strokes, by stroke ID

// Each person keeps the same color for their cursor and half-drawn strokes
const palette = ['#7F77DD', '#D85A30', '#1D9E75', '#378ADD', '#BA7517', '#D4537E'];
function colorOf(personId: string) {
    let hash = 0;
    for (const c of personId) hash = (hash * 31 + c.charCodeAt(0)) | 0;
    return palette[Math.abs(hash) % palette.length];
}

// An arrow pointer like the one on your own screen, its tip at (0, 0)
const POINTER = 'M0 0 L0 17 L4.6 12.6 L7.6 19.4 L10.4 18.2 L7.4 11.6 L13.4 11.6 Z';

// Cursors are drawn in board units, so undo the zoom to keep them the size of a real pointer
const keepScreenSize = (group: Konva.Group) => group.scale({ x: 1 / stage.scaleX(), y: 1 / stage.scaleY() });

function showCursor(personId: string, x: number, y: number) {
    let cursor = cursors.get(personId);
    if (!cursor) {
        const group = new Konva.Group();
        group.add(new Konva.Path({ data: POINTER, fill: colorOf(personId), stroke: '#ffffff', strokeWidth: 1.5, lineJoin: 'round' }));
        group.add(new Konva.Label({ x: 12, y: 18 })
            .add(new Konva.Tag({ fill: colorOf(personId), cornerRadius: 4 }))
            .add(new Konva.Text({ text: people.get(personId)?.name ?? '…', fontSize: 12, padding: 4, fill: '#ffffff' })));
        others.add(group);
        cursor = { group, hide: 0 };
        cursors.set(personId, cursor);
    }
    keepScreenSize(cursor.group);
    cursor.group.position({ x, y }).visible(true);
    // Hide it when they stop moving, so a cursor isn't left sitting on the board
    clearTimeout(cursor.hide);
    cursor.hide = window.setTimeout(() => {
        cursor!.group.visible(false);
        showPointers();
    }, 4000);
    showPointers();
}

// And when you zoom, every cursor already showing
stage.on('scaleXChange', () => cursors.forEach(c => keepScreenSize(c.group)));

// Someone whose cursor is off the screen gets a marker at the edge pointing their way, since the board has no end to
// lose them on. Clicking it goes there.
const pointers = new Map<string, HTMLButtonElement>();
const EDGE_INSET = 24;                                     // from the edge to the marker's middle, leaving room for its arrow

function showPointers() {
    const width = stage.width(), height = stage.height();
    const middle = { x: width / 2, y: height / 2 };
    const transform = stage.getAbsoluteTransform();
    for (const id of new Set([...cursors.keys(), ...pointers.keys()])) {
        const cursor = cursors.get(id);
        const at = cursor && transform.point(cursor.group.position());
        let marker = pointers.get(id);
        if (!cursor || !at || !cursor.group.visible() || (at.x >= 0 && at.y >= 0 && at.x <= width && at.y <= height)) {
            marker?.remove();
            pointers.delete(id);
            continue;
        }

        if (!marker) {
            const name = people.get(id)?.name ?? 'Someone';
            marker = Object.assign(document.createElement('button'), {
                type: 'button', className: 'offscreen-cursor', textContent: [...name][0]?.toUpperCase() ?? '?', title: `Go to ${name}`,
            });
            marker.style.setProperty('--who', colorOf(id));
            marker.addEventListener('click', () => {
                const where = cursors.get(id)?.group.position();
                if (where) lookAt(where);
            });
            container.append(marker);
            pointers.set(id, marker);
        }

        // Where the line from the middle of the box towards them crosses the edge (pulled in a little)
        const dx = at.x - middle.x, dy = at.y - middle.y;
        const reach = Math.min((middle.x - EDGE_INSET) / Math.abs(dx || 1e-9), (middle.y - EDGE_INSET) / Math.abs(dy || 1e-9));
        marker.style.left = `${middle.x + dx * reach}px`;
        marker.style.top = `${middle.y + dy * reach}px`;
        marker.style.setProperty('--angle', `${Math.atan2(dy, dx)}rad`);
    }
}
stage.on('xChange yChange scaleXChange widthChange heightChange', showPointers);

function showPeople(list: CanvasPerson[]) {
    people.clear();
    for (const person of list) people.set(person.id, person);

    // Their cursors go when they leave
    for (const [id, cursor] of cursors)
        if (!people.has(id)) { cursor.group.destroy(); cursors.delete(id); }
    showPointers();

    document.querySelector('[data-here]')!.textContent = `${list.length} here`;
    const box = document.querySelector('[data-people]')!;
    box.replaceChildren(...list.map(person => {
        const el = person.avatarUrl ? Object.assign(document.createElement('img'), { src: person.avatarUrl, alt: '' })
            : Object.assign(document.createElement('span'), { className: 'initial', textContent: person.name[0] });
        el.title = person.id === live?.me.id ? `${person.name} (you)` : person.name;
        return el;
    }));
}

const status = document.querySelector<HTMLElement>('[data-status]')!;
const statusDot = status.querySelector<HTMLElement>('.live-dot')!;

function showStatus(state: 'live' | 'reconnecting' | 'offline', text?: string) {
    status.className = 'board-status ' + state;
    statusDot.classList.toggle('off', state !== 'live');
    status.querySelector('[data-status-text]')!.textContent = text ?? { live: 'Live', reconnecting: 'Reconnecting…', offline: 'Offline' }[state];
}

// A flash of the dot when someone else changes the board, like the top bar's when the bot changes something
function pulse() {
    statusDot.classList.remove('pulse');
    void statusDot.offsetWidth;
    statusDot.classList.add('pulse');
}

// Everything above exists now, so the tools can be set up
setTool('move');

// ---- Connecting ----------------------------------------------------------------------------------------------------

// Leaving by a link (like the back arrow): tell the server first, or the page you land on (like the canvases list)
// can be drawn before it notices the connection closed, and still count you as here. Half a second at most.
document.addEventListener('click', e => {
    const link = (e.target as HTMLElement).closest<HTMLAnchorElement>('a[href]');
    if (!link || !live || e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
    if (link.target === '_blank' || new URL(link.href).origin !== location.origin) return;
    e.preventDefault();
    Promise.race([live.leave(), new Promise(resolve => setTimeout(resolve, 500))])
        .finally(() => { location.href = link.href; });
});

// The browser's own Back button or closing the tab can't be held up, so just disconnect as the page goes
window.addEventListener('pagehide', () => { live?.stop(); });
// Coming back to a page the browser kept in memory, the connection is gone; load it fresh
window.addEventListener('pageshow', e => { if (e.persisted) location.reload(); });

let live: CanvasLive | null = null;
try {
    live = await connect(canvasId, {
        load: items => {
            load(items);
            if (!viewMoved) fitView();
            // A canvas without a picture yet (like one made before thumbnails) gets one when it's first opened
            if (!container.dataset.hasThumbnail && items.length) thumbnailSoon();
        },
        changed: thumbnailSoon,
        presence: showPeople,
        status: showStatus,
        cursor: showCursor,
        strokeProgress: (personId, strokeId, points) => {
            let line = drafts.get(strokeId);
            if (!line) {
                line = new Konva.Line({ points: [], stroke: colorOf(personId), strokeWidth: 4, lineCap: 'round', lineJoin: 'round', tension: 0.5, opacity: 0.6 });
                others.add(line);
                drafts.set(strokeId, line);
            }
            line.points(line.points().concat(points));
        },
        strokeEnd: (_, strokeId, json) => {
            drafts.get(strokeId)?.destroy();
            drafts.delete(strokeId);
            addItem(strokeId, 'stroke', json);
            pulse();
        },
        itemAdded: (personId, itemId, type, json) => { addItem(itemId, type, json, personId); pulse(); },
        itemMoved: (_, itemId, x, y) => stage.findOne('#' + itemId)?.position({ x, y }),
        // Moves aren't flashed: a drag sends a dozen a second, which would keep it lit
        itemRemoved: (_, itemId) => { const node = stage.findOne('#' + itemId); if (node) removeItem(node); pulse(); },
    });
} catch {
    // You can still draw, but nothing is saved or shared
    showStatus('offline', "Couldn't connect. Drawings won't be saved.");
}
