import { setWorkerUrl } from 'maplibre-gl'
import workerUrl from 'maplibre-gl/dist/maplibre-gl-worker.mjs?worker&url'

// O MapLibre monta o worker com `new URL('./maplibre-gl-worker.mjs',
// import.meta.url)`. Em dev isso cai no arquivo real dentro de node_modules; no
// build, `import.meta.url` é o chunk em /assets/, e o worker vira um 404. O
// mapa sobe, os marcadores aparecem e nenhum tile renderiza — porque quem
// decodifica tile é o worker.
//
// `?worker&url` faz o Vite EMPACOTAR o worker — com o maplibre-gl-shared que
// ele importa — e devolver a URL do arquivo gerado. Com `?url` puro o arquivo
// seria copiado cru e o import interno dele viraria outro 404.
//
// Import por efeito colateral: todo componente que monta um MapLibreMap
// importa este módulo uma vez, antes de criar o mapa — setWorkerUrl é
// idempotente, então múltiplos componentes importando não é problema.
setWorkerUrl(workerUrl)
