import { createTimeline, stagger } from './vendor/anime.esm.min.js';

const activeTimelines = new WeakMap();
const resolutionStartMs = 2200;

export function playBattleResolution(root, options = {}) {
    if (!root) {
        return;
    }

    cancelBattleResolution(root);

    const reducedMotion = options.reducedMotion === true
        || window.matchMedia('(prefers-reduced-motion: reduce)').matches;

    if (reducedMotion) {
        setFinalState(root);
        return;
    }

    setInitialState(root);

    const fieldLines = root.querySelectorAll('.battle-resolution__field-line');
    const yourSide = root.querySelector('.battle-resolution__side--yours');
    const opponentSide = root.querySelector('.battle-resolution__side--opponent');
    const moveBadges = root.querySelectorAll('.battle-resolution__move');
    const winningMove = root.querySelector('.battle-resolution__move--winner');
    const impact = root.querySelector('.battle-resolution__impact');
    const shockwave = root.querySelector('.battle-resolution__shockwave');
    const winningPlay = root.querySelector('.battle-resolution__winning-play');
    const method = root.querySelector('.battle-resolution__method');
    const outcome = root.querySelector('.battle-resolution__outcome');
    const actions = root.querySelector('.battle-resolution__actions');
    const prelude = getWinningMovePrelude(root);
    const winningMoveKey = normalizeMoveKey(options.winningMove || prelude.intro?.dataset.winningMove);

    try {
        root.focus({ preventScroll: true });

        const timeline = createTimeline({
            defaults: {
                duration: 700,
                ease: 'out(3)'
            },
            onComplete: () => {
                root.dataset.animationState = 'complete';
                activeTimelines.delete(root);
            }
        });

        // The move prelude runs first, then the existing broadcast-style result bumper takes over.
        timeline.add(root, { opacity: [0, 1], duration: 260, ease: 'out(1)' }, 0);
        addWinningMovePrelude(timeline, prelude, winningMoveKey);

        timeline
            .add(fieldLines, { opacity: [0, 0.75], scaleX: [0.18, 1], delay: stagger(140), duration: 760 }, resolutionStartMs + 120)
            .add(yourSide, { opacity: [0, 1], x: ['-34vw', '0vw'], scale: [0.94, 1], duration: 820 }, resolutionStartMs + 560)
            .add(opponentSide, { opacity: [0, 1], x: ['34vw', '0vw'], scale: [0.94, 1], duration: 820 }, resolutionStartMs + 720)
            .add(moveBadges, { opacity: [0, 1], y: ['0.95rem', '0rem'], scale: [0.88, 1], delay: stagger(160), duration: 620 }, resolutionStartMs + 1400)
            .add(winningMove, { scale: [1, 1.12, 1.02, 1.08, 1], filter: ['brightness(1)', 'brightness(1.45)', 'brightness(1.08)', 'brightness(1.32)', 'brightness(1)'], duration: 980, ease: 'out(2)' }, resolutionStartMs + 2140)
            .add(impact, { opacity: [0, 1], scale: [0.66, 1.16, 1], rotate: ['-10deg', '3deg', '0deg'], duration: 820 }, resolutionStartMs + 2660)
            .add(shockwave, { opacity: [0.62, 0], scale: [0.42, 2.65], duration: 900, ease: 'out(2)' }, resolutionStartMs + 2760)
            .add(winningPlay, { opacity: [0, 1], y: ['0.9rem', '0rem'], scale: [0.84, 1.04, 1], filter: ['brightness(1.25)', 'brightness(1)'], duration: 720 }, resolutionStartMs + 3200)
            .add(method, { opacity: [0, 1], y: ['1.1rem', '0rem'], duration: 620 }, resolutionStartMs + 3520)
            .add(outcome, { opacity: [0, 1], y: ['1.2rem', '0rem'], scale: [0.92, 1.04, 1], filter: ['brightness(1.2)', 'brightness(1)'], duration: 760 }, resolutionStartMs + 3860)
            .add(actions, { opacity: [0, 1], y: ['0.7rem', '0rem'], duration: 420 }, resolutionStartMs + 4300);

        activeTimelines.set(root, timeline);
    } catch (error) {
        setFinalState(root);
        throw error;
    }
}

export function cancelBattleResolution(root) {
    const timeline = activeTimelines.get(root);
    if (!timeline) {
        return;
    }

    if (typeof timeline.cancel === 'function') {
        timeline.cancel();
    } else if (typeof timeline.pause === 'function') {
        timeline.pause();
    }

    activeTimelines.delete(root);
    setFinalState(root);
}

function addWinningMovePrelude(timeline, elements, moveKey) {
    if (!elements.intro) {
        return;
    }

    const start = 80;
    const moveStart = 420;
    const outroStart = 1840;

    add(timeline, elements.intro, { opacity: [0, 1], scale: [0.96, 1], duration: 260, ease: 'out(2)' }, start);
    add(timeline, elements.spotlight, { opacity: [0, 1], y: ['0.8rem', '0rem'], scale: [0.98, 1], duration: 360 }, start + 80);
    add(timeline, elements.field, { opacity: [0, 1], filter: ['brightness(0.82)', 'brightness(1)'], duration: 320 }, start + 120);
    add(timeline, elements.label, { opacity: [0, 1], y: ['0.65rem', '0rem'], duration: 420 }, start + 260);

    switch (moveKey) {
        case 'paper':
            addCutOutPassPrelude(timeline, elements, moveStart);
            break;
        case 'scissors':
            addGrubberKickPrelude(timeline, elements, moveStart);
            break;
        case 'lizard':
            addSidestepPrelude(timeline, elements, moveStart);
            break;
        case 'spock':
            addScrumDrivePrelude(timeline, elements, moveStart);
            break;
        case 'selection':
            addSelectionPrelude(timeline, elements, moveStart);
            break;
        case 'rock':
        default:
            addCrashBallPrelude(timeline, elements, moveStart);
            break;
    }

    add(timeline, elements.intro, { opacity: [1, 0], scale: [1, 1.04], duration: 340, ease: 'out(2)' }, outroStart);
}

function addCrashBallPrelude(timeline, elements, start) {
    add(timeline, [elements.attacker, elements.ball], { opacity: [0, 1], x: ['-9rem', '-9rem'], y: ['0rem', '0rem'], duration: 120 }, start);
    add(timeline, elements.defender, { opacity: [0, 1], x: ['7.5rem', '7.5rem'], y: ['0rem', '0rem'], duration: 140 }, start + 120);
    add(timeline, elements.primaryPath, { opacity: [0, 0.9], scaleX: [0, 1], rotate: ['0deg', '0deg'], duration: 540 }, start + 260);
    add(timeline, [elements.attacker, elements.ball], { x: ['-9rem', '3.2rem'], scale: [1, 1.1], duration: 720 }, start + 300);
    add(timeline, elements.defender, { x: ['7.5rem', '5.2rem'], rotate: ['0deg', '-14deg'], duration: 460 }, start + 620);
    add(timeline, elements.burst, { opacity: [0, 1, 0], scale: [0.35, 1.75], duration: 540, ease: 'out(2)' }, start + 820);
    add(timeline, [elements.attacker, elements.ball], { x: ['3.2rem', '7.5rem'], scale: [1.08, 1], duration: 420 }, start + 1080);
}

function addCutOutPassPrelude(timeline, elements, start) {
    add(timeline, elements.attacker, { opacity: [0, 1], x: ['-8rem', '-5.2rem'], y: ['0rem', '0rem'], duration: 320 }, start);
    add(timeline, elements.defender, { opacity: [0, 1], x: ['1.6rem', '0.4rem'], y: ['0rem', '0rem'], duration: 380 }, start + 120);
    add(timeline, elements.support, { opacity: [0, 1], x: ['6.8rem', '7.2rem'], y: ['-3.5rem', '-3.5rem'], duration: 260 }, start + 220);
    add(timeline, elements.primaryPath, { opacity: [0, 0.9], scaleX: [0, 1], rotate: ['-24deg', '-24deg'], duration: 600 }, start + 360);
    add(timeline, elements.ball, { opacity: [0, 1], x: ['-4.8rem', '6.8rem'], y: ['0rem', '-3.5rem'], duration: 760 }, start + 420);
    add(timeline, elements.secondaryPath, { opacity: [0, 0.64], scaleX: [0, 0.7], rotate: ['8deg', '8deg'], duration: 420 }, start + 680);
    add(timeline, elements.burst, { opacity: [0, 0.85, 0], x: ['7.1rem', '7.1rem'], y: ['-3.5rem', '-3.5rem'], scale: [0.3, 1.25], duration: 520 }, start + 980);
}

function addGrubberKickPrelude(timeline, elements, start) {
    add(timeline, elements.attacker, { opacity: [0, 1], x: ['-8rem', '-6rem'], y: ['1.8rem', '1.8rem'], duration: 280 }, start);
    add(timeline, elements.defender, { opacity: [0, 1], x: ['1.2rem', '1.2rem'], y: ['1.2rem', '1.2rem'], duration: 220 }, start + 120);
    add(timeline, elements.primaryPath, { opacity: [0, 0.9], scaleX: [0, 1], rotate: ['8deg', '8deg'], duration: 720 }, start + 300);
    add(timeline, elements.ball, { opacity: [0, 1], x: ['-5.9rem', '8.2rem'], y: ['2rem', '3.2rem'], scale: [1, 0.82, 1], duration: 900 }, start + 360);
    add(timeline, elements.defender, { y: ['1.2rem', '-1.1rem', '1.2rem'], rotate: ['0deg', '10deg', '0deg'], duration: 620 }, start + 520);
    add(timeline, elements.burst, { opacity: [0, 0.8, 0], x: ['8.2rem', '8.2rem'], y: ['3.2rem', '3.2rem'], scale: [0.25, 1.35], duration: 500 }, start + 1040);
}

function addSidestepPrelude(timeline, elements, start) {
    add(timeline, [elements.attacker, elements.ball], { opacity: [0, 1], x: ['-8rem', '-8rem'], y: ['2.4rem', '2.4rem'], duration: 120 }, start);
    add(timeline, elements.defender, { opacity: [0, 1], x: ['0.5rem', '0.5rem'], y: ['0rem', '0rem'], duration: 160 }, start + 120);
    add(timeline, elements.primaryPath, { opacity: [0, 0.85], scaleX: [0, 0.55], rotate: ['-28deg', '-28deg'], duration: 360 }, start + 260);
    add(timeline, [elements.attacker, elements.ball], { x: ['-8rem', '-2.2rem'], y: ['2.4rem', '-2.2rem'], duration: 420 }, start + 300);
    add(timeline, elements.secondaryPath, { opacity: [0, 0.78], scaleX: [0, 0.7], rotate: ['28deg', '28deg'], duration: 360 }, start + 620);
    add(timeline, [elements.attacker, elements.ball], { x: ['-2.2rem', '5.8rem'], y: ['-2.2rem', '0.8rem'], scale: [1.05, 1], duration: 560 }, start + 660);
    add(timeline, elements.defender, { x: ['0.5rem', '-0.8rem'], y: ['0rem', '1.5rem'], rotate: ['0deg', '-18deg'], duration: 480 }, start + 760);
    add(timeline, elements.burst, { opacity: [0, 0.82, 0], x: ['0.3rem', '0.3rem'], y: ['0rem', '0rem'], scale: [0.3, 1.5], duration: 520 }, start + 920);
}

function addScrumDrivePrelude(timeline, elements, start) {
    add(timeline, elements.attacker, { opacity: [0, 1], x: ['-8rem', '-8rem'], y: ['-0.9rem', '-0.9rem'], duration: 120 }, start);
    add(timeline, elements.support, { opacity: [0, 1], x: ['-8rem', '-8rem'], y: ['1rem', '1rem'], duration: 120 }, start + 80);
    add(timeline, elements.defender, { opacity: [0, 1], x: ['3.7rem', '3.7rem'], y: ['0rem', '0rem'], duration: 160 }, start + 140);
    add(timeline, elements.ball, { opacity: [0, 1], x: ['-9.4rem', '-9.4rem'], y: ['0.1rem', '0.1rem'], duration: 140 }, start + 180);
    add(timeline, elements.primaryPath, { opacity: [0, 0.9], scaleX: [0, 0.86], rotate: ['0deg', '0deg'], duration: 640 }, start + 320);
    add(timeline, [elements.attacker, elements.support, elements.ball], { x: ['-8rem', '2.4rem'], scale: [1, 1.08], duration: 760 }, start + 360);
    add(timeline, elements.defender, { x: ['3.7rem', '6.8rem'], scale: [1, 0.92], duration: 760 }, start + 440);
    add(timeline, elements.burst, { opacity: [0, 1, 0], x: ['3.8rem', '3.8rem'], y: ['0rem', '0rem'], scale: [0.4, 1.9], duration: 620 }, start + 900);
}

function addSelectionPrelude(timeline, elements, start) {
    add(timeline, elements.ball, { opacity: [0, 1], x: ['0rem', '0rem'], y: ['0rem', '0rem'], scale: [0.82, 1.08, 1], duration: 620 }, start + 160);
    add(timeline, elements.primaryPath, { opacity: [0, 0.65], scaleX: [0, 0.45], rotate: ['0deg', '0deg'], duration: 560 }, start + 260);
    add(timeline, elements.burst, { opacity: [0, 0.72, 0], scale: [0.35, 1.45], duration: 620 }, start + 680);
}

function getWinningMovePrelude(root) {
    return {
        intro: root.querySelector('.battle-resolution__move-intro'),
        spotlight: root.querySelector('.battle-resolution__move-intro-spotlight'),
        field: root.querySelector('.battle-resolution__move-intro-field'),
        label: root.querySelector('.battle-resolution__move-intro-label'),
        primaryPath: root.querySelector('.battle-resolution__move-path--primary'),
        secondaryPath: root.querySelector('.battle-resolution__move-path--secondary'),
        attacker: root.querySelector('.battle-resolution__move-token--attacker'),
        support: root.querySelector('.battle-resolution__move-token--support'),
        defender: root.querySelector('.battle-resolution__move-token--defender'),
        ball: root.querySelector('.battle-resolution__move-ball'),
        burst: root.querySelector('.battle-resolution__move-burst')
    };
}

function setInitialState(root) {
    root.dataset.animationState = 'running';
    root.style.opacity = '0';

    const prelude = getWinningMovePrelude(root);
    setStyles(prelude.intro, {
        display: 'grid',
        opacity: '0',
        transform: 'scale(0.96)'
    });
    setStyles(prelude.spotlight, {
        opacity: '0',
        transform: 'translate3d(0, 0.8rem, 0) scale(0.98)'
    });
    setStyles([prelude.field, prelude.label], {
        opacity: '0',
        transform: ''
    });
    setStyles([prelude.primaryPath, prelude.secondaryPath], {
        opacity: '0',
        transform: 'scaleX(0)'
    });
    setStyles([prelude.attacker, prelude.support, prelude.defender, prelude.ball, prelude.burst], {
        opacity: '0',
        transform: ''
    });

    setStyles(root.querySelectorAll('.battle-resolution__field-line'), {
        opacity: '0',
        transform: 'scaleX(0.25)'
    });
    setStyles(root.querySelector('.battle-resolution__side--yours'), {
        opacity: '0',
        transform: 'translate3d(-28vw, 0, 0) scale(0.96)'
    });
    setStyles(root.querySelector('.battle-resolution__side--opponent'), {
        opacity: '0',
        transform: 'translate3d(28vw, 0, 0) scale(0.96)'
    });
    setStyles(root.querySelectorAll('.battle-resolution__move'), {
        opacity: '0',
        transform: 'translate3d(0, 0.85rem, 0) scale(0.9)'
    });
    setStyles(root.querySelector('.battle-resolution__impact'), {
        opacity: '0',
        transform: 'scale(0.7) rotate(-8deg)'
    });
    setStyles(root.querySelector('.battle-resolution__shockwave'), {
        opacity: '0',
        transform: 'scale(0.5)'
    });
    setStyles(root.querySelector('.battle-resolution__winning-play'), {
        opacity: '0',
        transform: 'translate3d(0, 0.9rem, 0) scale(0.84)'
    });
    setStyles(root.querySelector('.battle-resolution__method'), {
        opacity: '0',
        transform: 'translate3d(0, 1.1rem, 0)'
    });
    setStyles(root.querySelector('.battle-resolution__outcome'), {
        opacity: '0',
        transform: 'translate3d(0, 1.2rem, 0) scale(0.94)'
    });
    setStyles(root.querySelector('.battle-resolution__actions'), {
        opacity: '0',
        transform: 'translate3d(0, 0.7rem, 0)'
    });
}

function setFinalState(root) {
    root.dataset.animationState = 'complete';
    root.style.opacity = '1';

    const prelude = getWinningMovePrelude(root);
    setStyles(prelude.intro, {
        display: 'none',
        opacity: '',
        transform: '',
        filter: ''
    });
    setStyles([prelude.spotlight, prelude.field, prelude.label, prelude.primaryPath, prelude.secondaryPath, prelude.attacker, prelude.support, prelude.defender, prelude.ball, prelude.burst], {
        opacity: '',
        transform: '',
        filter: ''
    });
    setStyles(root.querySelectorAll('.battle-resolution__field-line'), {
        opacity: '',
        transform: ''
    });
    setStyles(root.querySelectorAll('.battle-resolution__side, .battle-resolution__move, .battle-resolution__impact, .battle-resolution__shockwave, .battle-resolution__winning-play, .battle-resolution__method, .battle-resolution__outcome, .battle-resolution__actions'), {
        opacity: '',
        transform: '',
        filter: ''
    });
}

function add(timeline, targets, params, position) {
    const elements = normalizeTargets(targets);
    if (elements.length > 0) {
        timeline.add(elements, params, position);
    }
}

function normalizeTargets(targets) {
    if (!targets) {
        return [];
    }

    if (targets instanceof Element) {
        return [targets];
    }

    return Array.from(targets).filter(Boolean);
}

function normalizeMoveKey(move) {
    const key = String(move || 'selection').toLowerCase();
    return ['rock', 'paper', 'scissors', 'lizard', 'spock'].includes(key)
        ? key
        : 'selection';
}

function setStyles(targets, styles) {
    if (!targets) {
        return;
    }

    const elements = targets instanceof Element ? [targets] : Array.from(targets).filter(Boolean);
    for (const element of elements) {
        for (const [name, value] of Object.entries(styles)) {
            element.style[name] = value;
        }
    }
}
