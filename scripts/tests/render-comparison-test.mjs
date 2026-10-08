import assert from 'node:assert/strict';
import { readFileSync } from 'node:fs';
import vm from 'node:vm';
const context = vm.createContext({});
vm.runInContext(readFileSync(new URL('../../src/STS2Mobile/Launcher/RenderComparison.js', import.meta.url), 'utf8'), context);
const { makePairs } = context;

const result = (Case, Variant, Screenshot = `${Variant}.png`) => ({ Case, Variant, Screenshot });
const cardsStart = result('CombatCards', 'baseline-start');
const cardsEnd = result('CombatCards', 'baseline-end');
const direct = result('CombatCards', 'direct portraits');
const effectsStart = result('CombatEffects', 'baseline-start');
const blur = result('CombatEffects', 'blur off');
const pairs = makePairs([
  cardsStart, direct, cardsEnd, effectsStart, blur,
  result('CombatCards', 'pacing 2, FPS 120'),
  result('Map', 'MSAA 2x'),
  result('CombatCards', 'mipmap', null),
]);
assert.equal(pairs.length, 2, 'Only captured quality variants with a same-scene baseline can be paired');
assert.equal(pairs[0].variant, direct);
assert.equal(pairs[0].start, cardsStart);
assert.equal(pairs[0].end, cardsEnd);
assert.equal(pairs[1].start, effectsStart, 'Never use a baseline from another scene');
assert.equal(pairs[1].end, null, 'Partial runs may lack the ending baseline');
assert.equal(makePairs([]).length, 0);
console.log('PASS same-scene comparison pairing and partial runs');
