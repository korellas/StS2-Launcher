function makePairs(results) {
  const pairs = [];
  for (const variant of results) {
    if (!variant.Screenshot || variant.Variant === 'baseline-start' ||
        variant.Variant === 'baseline-end' || variant.Variant.startsWith('pacing ')) continue;
    const baselines = results.filter(item => item.Case === variant.Case && item.Screenshot);
    const start = baselines.find(item => item.Variant === 'baseline-start');
    const end = baselines.find(item => item.Variant === 'baseline-end');
    if (start || end) pairs.push({ variant, start: start || null, end: end || null });
  }
  return pairs;
}

function differenceStats(histogram, threshold) {
  let total = 0, changed = 0;
  histogram.forEach((count, delta) => {
    total += count;
    if (delta > threshold) changed += count;
  });
  return { total, changed, percent: total ? changed * 100 / total : 0 };
}

function startComparison() {
  const byId = id => document.getElementById(id);
  const data = JSON.parse(byId('benchmark-data').textContent);
  const pairs = makePairs(data.Results);
  const scene = byId('scene'), variant = byId('variant'), baseline = byId('baseline');
  const views = [byId('view-a'), byId('view-b')];
  const images = [byId('image-a'), byId('image-b')];
  const diffImage = byId('diff-image'), diffToggle = byId('diff-toggle');
  const threshold = byId('diff-threshold'), gain = byId('diff-gain');
  let difference = null, showingDiff = false;
  let zoom = 1, fitting = false, single = false, showChanged = false;
  let generation = 0;
  let focus = { x: .5, y: .5 };
  const sceneNames = { CombatCards: '손패·카드 확대', CombatEffects: '전투 효과', Merchant: '상점' };
  const optionNames = {
    nearest: '최근접 필터', mipmap: '선형 + 밉맵', anisotropic: '밉맵 + 이방성',
    'direct portraits': '초상화 직접 그리기', 'blur off': '방사형 블러 끄기',
  };
  const optionName = name => optionNames[name] || name;
  const displayedImages = () => [images[0], showingDiff && difference ? diffImage : images[1]];
  byId('environment').textContent = `App ${data.App} · ${data.Device} · ${data.Resolution} · ${data.StartedUtc}`;

  function fill(select, options) {
    select.replaceChildren(...options.map(([value, text]) => new Option(text, value)));
  }
  fill(scene, [...new Set(pairs.map(pair => pair.variant.Case))].map(name => [name, sceneNames[name] || name]));
  function readFocus(view, image) {
    if (image.width && image.height) focus = {
      x: (view.scrollLeft + view.clientWidth / 2) / Math.max(image.width, view.clientWidth),
      y: (view.scrollTop + view.clientHeight / 2) / Math.max(image.height, view.clientHeight),
    };
  }
  function position(index) {
    const view = views[index], image = displayedImages()[index];
    view.scrollLeft = focus.x * Math.max(image.width, view.clientWidth) - view.clientWidth / 2;
    view.scrollTop = focus.y * Math.max(image.height, view.clientHeight) - view.clientHeight / 2;
  }
  function layout() {
    const displayed = displayedImages();
    if (!displayed.every(image => image.naturalWidth)) return;
    const ratio = window.devicePixelRatio || 1;
    if (fitting) zoom = Math.min(...displayed.flatMap((image, index) =>
      views[index].parentElement.hidden ? [] : [Math.min(views[index].clientWidth / image.naturalWidth, views[index].clientHeight / image.naturalHeight) * ratio]));
    for (let i = 0; i < displayed.length; i++) {
      const image = displayed[i], stage = image.parentElement;
      image.style.width = `${image.naturalWidth * zoom / ratio}px`;
      image.style.height = `${image.naturalHeight * zoom / ratio}px`;
      stage.style.width = `${Math.max(image.width, views[i].clientWidth)}px`;
      stage.style.height = `${Math.max(image.height, views[i].clientHeight)}px`;
      position(i);
    }
    byId('zoom100').setAttribute('aria-pressed', String(!fitting && zoom === 1));
    byId('zoom200').setAttribute('aria-pressed', String(!fitting && zoom === 2));
    byId('fit').setAttribute('aria-pressed', String(fitting));
    byId('details').textContent = `${Math.round(zoom * 100)}% · 원본 ${images[0].naturalWidth}×${images[0].naturalHeight} · 변경 ${images[1].naturalWidth}×${images[1].naturalHeight}`;
  }
  function updateDifference() {
    const limit = Number(threshold.value), multiplier = Number(gain.value);
    byId('diff-threshold-value').textContent = String(limit);
    const colors = Array.from({ length: 256 }, (_, delta) => delta > limit ? Math.min(1, delta * multiplier / 255) : 0);
    byId('diff-red').setAttribute('tableValues', colors.join(' '));
    byId('diff-green').setAttribute('tableValues', colors.map(value => value * .5).join(' '));
    const visible = showingDiff && !!difference;
    images[1].hidden = visible;
    diffImage.hidden = !visible;
    diffToggle.disabled = !difference;
    diffToggle.setAttribute('aria-pressed', String(visible));
    const pair = pairs[Number(variant.value)];
    if (pair) byId('label-b').textContent = visible ? `B · 픽셀 차이 · 강조 ×${multiplier}` : `B · ${optionName(pair.variant.Variant)}`;
    if (difference) {
      const stats = differenceStats(difference.Histogram, limit);
      byId('diff-stats').textContent = `변경 픽셀(차이 > ${limit}) ${stats.percent.toFixed(2)}% · ${stats.changed.toLocaleString()} / ${stats.total.toLocaleString()} · 채널 평균 차이 ${difference.MeanAbsolute.toFixed(3)}/255 · 최대 ${difference.Maximum}/255`;
    } else byId('diff-stats').textContent = '같은 크기의 차이 이미지가 없습니다. 앱에서 저장된 비교를 다시 열어 주세요.';
  }
  async function selectPair() {
    const current = pairs[Number(variant.value)];
    if (!current) return;
    const original = current[baseline.value] || current.start || current.end;
    const currentGeneration = ++generation;
    difference = null;
    updateDifference();
    byId('diff-stats').textContent = '차이 이미지를 불러오는 중…';
    baseline.options[0].disabled = !current.start;
    baseline.options[1].disabled = !current.end;
    baseline.value = original === current.start ? 'start' : 'end';
    byId('label-a').textContent = `A · 원본 · ${original.Variant}`;
    byId('label-b').textContent = `B · ${optionName(current.variant.Variant)}`;
    byId('error').textContent = '';
    images[0].src = original.Screenshot;
    images[1].src = current.variant.Screenshot;
    try {
      await Promise.all(images.map(image => image.decode()));
      if (currentGeneration !== generation) return;
      if (images[0].naturalWidth !== images[1].naturalWidth || images[0].naturalHeight !== images[1].naturalHeight)
        byId('error').textContent = '캡처 크기가 달라 동일한 상대 위치로 이동합니다.';
      const selectedDifference = original === current.start ? current.variant.DiffStart : current.variant.DiffEnd;
      if (selectedDifference) {
        diffImage.src = selectedDifference.Screenshot;
        try {
          await diffImage.decode();
          if (currentGeneration !== generation) return;
          difference = selectedDifference;
        } catch (error) {
          if (currentGeneration !== generation) return;
          byId('error').textContent = '차이 이미지를 열 수 없습니다. 앱에서 저장된 비교를 다시 열어 주세요.';
        }
      }
      updateDifference();
      focus = { x: .5, y: .5 };
      layout();
    } catch (error) {
      if (currentGeneration === generation) byId('error').textContent = '캡처 파일을 열 수 없습니다. 앱에서 비교 화면을 다시 만들어 주세요.';
    }
  }
  function selectScene() {
    fill(variant, pairs.flatMap((pair, index) => pair.variant.Case === scene.value ? [[String(index), optionName(pair.variant.Variant)]] : []));
    selectPair();
  }
  scene.onchange = selectScene;
  variant.onchange = baseline.onchange = selectPair;
  diffToggle.onclick = () => { showingDiff = !showingDiff; updateDifference(); layout(); };
  threshold.oninput = gain.onchange = updateDifference;
  for (const [id, scale] of [['zoom100', 1], ['zoom200', 2]]) byId(id).onclick = () => {
    fitting = false; zoom = scale; layout();
  };
  byId('fit').onclick = () => { fitting = true; focus = { x: .5, y: .5 }; layout(); };
  byId('center').onclick = () => { focus = { x: .5, y: .5 }; views.forEach((_, index) => position(index)); };
  byId('toggle').onclick = () => {
    if (!single) { single = true; showChanged = false; }
    else if (!showChanged) showChanged = true;
    else single = false;
    views[0].parentElement.hidden = single && showChanged;
    views[1].parentElement.hidden = single && !showChanged;
    byId('comparison').style.gridTemplateColumns = single ? '1fr' : '1fr 1fr';
    byId('toggle').textContent = single ? (showChanged ? '나란히 보기' : 'B로 전환') : 'A/B 전환';
    layout();
  };
  views.forEach((view, index) => {
    view.addEventListener('scroll', () => {
      const other = views[1 - index];
      if (view.parentElement.hidden) return;
      if (other.scrollLeft === view.scrollLeft && other.scrollTop === view.scrollTop) return;
      readFocus(view, displayedImages()[index]);
      if (!other.parentElement.hidden) position(1 - index);
    });
    let drag;
    view.addEventListener('pointerdown', event => {
      if (event.pointerType !== 'mouse' || event.button !== 0) return;
      drag = { x: event.clientX, y: event.clientY, left: view.scrollLeft, top: view.scrollTop };
      view.setPointerCapture(event.pointerId);
      view.classList.add('dragging');
    });
    view.addEventListener('pointermove', event => {
      if (!drag) return;
      view.scrollLeft = drag.left + drag.x - event.clientX;
      view.scrollTop = drag.top + drag.y - event.clientY;
    });
    for (const event of ['pointerup', 'pointercancel']) view.addEventListener(event, () => {
      drag = null; view.classList.remove('dragging');
    });
  });
  window.addEventListener('resize', () => { if (images.every(image => image.naturalWidth)) layout(); });
  if (pairs.length) selectScene();
  else byId('error').textContent = '비교할 원본과 옵션 캡처가 없습니다. 앱에서 비교 화면을 만들어 주세요.';
}

if (typeof document !== 'undefined') startComparison();
