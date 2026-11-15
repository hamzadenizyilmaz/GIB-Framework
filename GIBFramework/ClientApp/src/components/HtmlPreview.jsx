import { useEffect, useRef } from 'react';

export function HtmlPreview({ html, ...props }) {
  const frame = useRef(null);
  const render = () => frame.current?.contentWindow?.postMessage({ html, title: props.title }, '*');
  useEffect(render, [html, props.title]);
  return <iframe {...props} ref={frame} sandbox="allow-scripts" src="/preview/frame.html" onLoad={render} />;
}
