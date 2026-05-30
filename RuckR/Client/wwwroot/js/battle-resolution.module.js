import { createTimeline, stagger } from './vendor/anime.esm.min.js';

const activeTimelines = new WeakMap();

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

        // Broadcast-style pacing: the full bumper runs a little over four seconds.
        timeline
            .add(root, { opacity: [0, 1], duration: 260, ease: 'out(1)' }, 0)
            .add(fieldLines, { opacity: [0, 0.75], scaleX: [0.18, 1], delay: stagger(140), duration: 760 }, 120)
            .add(yourSide, { opacity: [0, 1], x: ['-34vw', '0vw'], scale: [0.94, 1], duration: 820 }, 560)
            .add(opponentSide, { opacity: [0, 1], x: ['34vw', '0vw'], scale: [0.94, 1], duration: 820 }, 720)
            .add(moveBadges, { opacity: [0, 1], y: ['0.95rem', '0rem'], scale: [0.88, 1], delay: stagger(160), duration: 620 }, 1400)
            .add(winningMove, { scale: [1, 1.12, 1.02, 1.08, 1], filter: ['brightness(1)', 'brightness(1.45)', 'brightness(1.08)', 'brightness(1.32)', 'brightness(1)'], duration: 980, ease: 'out(2)' }, 2140)
            .add(impact, { opacity: [0, 1], scale: [0.66, 1.16, 1], rotate: ['-10deg', '3deg', '0deg'], duration: 820 }, 2660)
            .add(shockwave, { opacity: [0.62, 0], scale: [0.42, 2.65], duration: 900, ease: 'out(2)' }, 2760)
            .add(winningPlay, { opacity: [0, 1], y: ['0.9rem', '0rem'], scale: [0.84, 1.04, 1], filter: ['brightness(1.25)', 'brightness(1)'], duration: 720 }, 3200)
            .add(method, { opacity: [0, 1], y: ['1.1rem', '0rem'], duration: 620 }, 3520)
            .add(outcome, { opacity: [0, 1], y: ['1.2rem', '0rem'], scale: [0.92, 1.04, 1], filter: ['brightness(1.2)', 'brightness(1)'], duration: 760 }, 3860)
            .add(actions, { opacity: [0, 1], y: ['0.7rem', '0rem'], duration: 420 }, 4300);

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

function setInitialState(root) {
    root.dataset.animationState = 'running';
    root.style.opacity = '0';

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

function setStyles(targets, styles) {
    if (!targets) {
        return;
    }

    const elements = targets instanceof Element ? [targets] : Array.from(targets);
    for (const element of elements) {
        for (const [name, value] of Object.entries(styles)) {
            element.style[name] = value;
        }
    }
}
