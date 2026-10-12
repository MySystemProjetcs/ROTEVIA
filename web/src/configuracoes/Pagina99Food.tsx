import { useCallback, useEffect, useState } from 'react'
import { useSessao } from '@/auth/SessaoProvider'
import { Botao } from '@/components/Botao'
import { Cartao, CartaoCorpo } from '@/components/Cartao'
import { api, ErroDaApi } from '@/lib/api'

interface ResultadoAutenticacao {
  sucesso: boolean
  appId99: string | null
  appShopId: string | null
  tokenMascarado: string | null
  erro: string | null
}

// Diagnóstico da integração com a 99Food (DiDi Food).
//
// O fluxo de pedidos de verdade continua entrando pelo webhook; esta página só
// existe para (a) vincular as credenciais da loja — app_shop_id + app_id_99 — e
// (b) conferir se a autenticação responde. O resultado espelha no console do
// navegador porque é ali que a validação está sendo feita hoje.
export function Pagina99Food() {
  const { usuario } = useSessao()
  const merchantId = usuario?.merchantId ?? null

  const [appShopId, setAppShopId] = useState('')
  const [appId99, setAppId99] = useState('')
  const [salvando, setSalvando] = useState(false)
  const [testando, setTestando] = useState(false)
  const [resultado, setResultado] = useState<ResultadoAutenticacao | null>(null)
  const [erro, setErro] = useState<string | null>(null)

  const testarAutenticacao = useCallback(async () => {
    if (!merchantId) return
    setTestando(true)
    setErro(null)
    try {
      const resposta = await api.post<ResultadoAutenticacao>(
        `/restaurantes/${merchantId}/99food/testar-autenticacao`,
      )
      setResultado(resposta)
      // Preenche os campos com o que já está salvo na loja, sem sobrescrever
      // o que o usuário digitar. O endpoint devolve app_shop_id/app_id_99
      // lidos direto da tabela de merchants, mesmo quando a 99 recusa o token.
      setAppShopId((atual) => (atual === '' ? resposta.appShopId ?? '' : atual))
      setAppId99((atual) => (atual === '' ? resposta.appId99 ?? '' : atual))
      // eslint-disable-next-line no-console
      console.log('[99Food] resultado da autenticação:', resposta)
    } catch (e) {
      const mensagem =
        e instanceof ErroDaApi ? e.message : 'Falha ao testar a autenticação da 99Food.'
      setErro(mensagem)
      // eslint-disable-next-line no-console
      console.warn('[99Food] falha ao autenticar:', e)
    } finally {
      setTestando(false)
    }
  }, [merchantId])

  // Uma execução ao abrir: o objetivo é ter o resultado no console sem clique.
  useEffect(() => {
    if (merchantId) void testarAutenticacao()
  }, [merchantId, testarAutenticacao])

  async function vincular(evento: React.FormEvent) {
    evento.preventDefault()
    if (!merchantId) return
    setSalvando(true)
    setErro(null)
    try {
      await api.put(`/restaurantes/${merchantId}/99food/app-shop-id`, {
        appShopId,
        appId99: appId99.trim() === '' ? null : appId99.trim(),
      })
      await testarAutenticacao()
    } catch (e) {
      setErro(e instanceof ErroDaApi ? e.message : 'Não foi possível vincular a loja.')
    } finally {
      setSalvando(false)
    }
  }

  if (!merchantId) {
    return <div className="p-6 text-texto-suave">Selecione uma loja para configurar a 99Food.</div>
  }

  return (
    <div className="flex flex-col gap-4">
      <Cartao>
        <CartaoCorpo className="flex flex-col gap-3">
          <div>
            <h2 className="text-titulo text-texto">Credenciais da 99Food</h2>
            <p className="mt-1 text-apoio text-texto-suave">
              O <span className="font-mono">app_shop_id</span> identifica a loja; o{' '}
              <span className="font-mono">app_id_99</span> é o id do app no painel da 99Food.
              Em branco, a autenticação usa o AppId global de configuração.
            </p>
          </div>

          <form className="flex flex-col gap-3 sm:flex-row sm:items-end" onSubmit={vincular}>
            <label className="flex flex-1 flex-col gap-1">
              <span className="text-rotulo uppercase text-texto-fraco">app_shop_id</span>
              <input
                type="text"
                value={appShopId}
                onChange={(e) => setAppShopId(e.target.value)}
                placeholder="Ex.: 1234"
                maxLength={64}
                className="rounded-controle border border-borda-forte bg-superficie px-3 py-2 text-apoio text-texto outline-offset-2 placeholder:text-texto-mudo focus-visible:outline-2 focus-visible:outline-marca-600"
              />
            </label>

            <label className="flex flex-1 flex-col gap-1">
              <span className="text-rotulo uppercase text-texto-fraco">app_id_99</span>
              <input
                type="text"
                value={appId99}
                onChange={(e) => setAppId99(e.target.value)}
                placeholder="opcional"
                maxLength={64}
                className="rounded-controle border border-borda-forte bg-superficie px-3 py-2 text-apoio text-texto outline-offset-2 placeholder:text-texto-mudo focus-visible:outline-2 focus-visible:outline-marca-600"
              />
            </label>

            <Botao type="submit" carregando={salvando} disabled={appShopId.trim() === ''}>
              Salvar e testar
            </Botao>
          </form>

          <div className="flex items-center gap-2">
            <Botao variante="secundario" tamanho="pequeno" carregando={testando} onClick={testarAutenticacao}>
              Testar autenticação
            </Botao>
            <span className="text-apoio text-texto-fraco">
              O resultado completo é impresso no console do navegador.
            </span>
          </div>

          {erro && <p className="text-apoio font-medium text-perigo">{erro}</p>}
        </CartaoCorpo>
      </Cartao>

      {resultado && (
        <Cartao>
          <CartaoCorpo className="flex flex-col gap-2">
            <h2 className="text-titulo text-texto">Último resultado</h2>
            <p className="text-apoio text-texto-suave">
              Status:{' '}
              <span className={resultado.sucesso ? 'font-semibold text-sucesso' : 'font-semibold text-perigo'}>
                {resultado.sucesso ? 'autenticado' : 'recusado'}
              </span>
            </p>
            <p className="text-apoio text-texto-suave">
              app_shop_id: <span className="font-mono text-texto">{resultado.appShopId ?? '—'}</span>
            </p>
            <p className="text-apoio text-texto-suave">
              app_id_99: <span className="font-mono text-texto">{resultado.appId99 ?? '(global)'}</span>
            </p>
            {resultado.tokenMascarado && (
              <p className="text-apoio text-texto-suave">
                auth_token: <span className="font-mono text-texto">{resultado.tokenMascarado}</span>
              </p>
            )}
            {resultado.erro && <p className="text-apoio text-perigo">{resultado.erro}</p>}
          </CartaoCorpo>
        </Cartao>
      )}
    </div>
  )
}
