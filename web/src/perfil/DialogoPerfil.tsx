import { useRef, useState } from 'react'
import type { ReactNode } from 'react'
import { AvatarUsuario } from '@/components/AvatarUsuario'
import { Dialogo } from '@/components/Dialogo'
import { Etiqueta } from '@/components/Etiqueta'
import { IconeX } from '@/components/icones/IconeX'
import { formatarDinheiro } from '@/lib/tempo'
import { usePerfil } from '@/perfil/contexto'
import { reduzirImagem } from '@/perfil/reduzirImagem'

const ROTULO_DO_PAPEL: Record<string, string> = {
  AdministradorSistema: 'Administrador',
  DonoRestaurante: 'Restaurante',
  Entregador: 'Motoboy',
}

function Linha({ rotulo, valor }: { rotulo: string; valor: ReactNode }) {
  return (
    <div className="flex items-baseline justify-between gap-4 border-b border-borda py-2 last:border-b-0">
      <span className="text-rotulo uppercase text-texto-suave">{rotulo}</span>
      <span className="truncate text-corpo text-texto">{valor}</span>
    </div>
  )
}

export function DialogoPerfil({ aberto, onFechar }: { aberto: boolean; onFechar: () => void }) {
  const { perfil, carregando, erro, enviarFoto } = usePerfil()
  const entradaRef = useRef<HTMLInputElement>(null)
  const [enviando, setEnviando] = useState(false)
  const [erroDaFoto, setErroDaFoto] = useState<string | null>(null)

  async function aoEscolherArquivo(arquivo: File | undefined) {
    if (!arquivo) return

    setErroDaFoto(null)
    setEnviando(true)

    try {
      await enviarFoto(await reduzirImagem(arquivo))
    } catch {
      // A mensagem da API já vem no `erro` do hook; aqui só cobre a falha de
      // leitura do arquivo, que não passa pela API.
      setErroDaFoto((atual) => atual ?? 'Não foi possível ler esta imagem.')
    } finally {
      setEnviando(false)
      // Limpa para o mesmo arquivo poder ser escolhido de novo: sem isso o
      // input não dispara change na segunda tentativa.
      if (entradaRef.current) entradaRef.current.value = ''
    }
  }

  const data = perfil ? new Date(perfil.membroDesde).toLocaleDateString('pt-BR') : ''

  return (
    <Dialogo aberto={aberto} onFechar={onFechar} titulo="Perfil">
      <div className="relative flex flex-col gap-4 p-5">
        {/* Canto superior direito: é onde se procura o fechar de uma janela, e
            libera a base do diálogo para o conteúdo. */}
        <button
          type="button"
          onClick={onFechar}
          aria-label="Fechar"
          className="absolute right-3 top-3 inline-flex size-8 items-center justify-center rounded-controle text-texto-suave outline-offset-2 transition-colors hover:bg-superficie-alt hover:text-texto focus-visible:outline-2 focus-visible:outline-marca-600"
        >
          <IconeX className="size-5" />
        </button>

        <div className="flex items-center gap-4 pr-8">
          {/* A própria foto é o botão de trocar: é onde a pessoa clica por
              instinto, e um botão separado ao lado seria alvo duplicado. */}
          <button
            type="button"
            onClick={() => entradaRef.current?.click()}
            disabled={enviando}
            className="group relative rounded-full outline-offset-2 focus-visible:outline-2 focus-visible:outline-marca-600"
          >
            <AvatarUsuario
              nome={perfil?.nome ?? ''}
              foto={perfil?.fotoBase64}
              className="size-16 text-titulo"
            />
            <span className="absolute inset-0 flex items-center justify-center rounded-full bg-texto/60 text-rotulo text-texto-invertido opacity-0 transition-opacity group-hover:opacity-100 group-focus-visible:opacity-100">
              {enviando ? 'Enviando…' : 'Trocar'}
            </span>
          </button>

          <input
            ref={entradaRef}
            type="file"
            accept="image/png,image/jpeg,image/webp"
            className="sr-only"
            onChange={(e) => void aoEscolherArquivo(e.target.files?.[0])}
          />

          <div className="flex min-w-0 flex-col gap-1">
            <p className="truncate text-titulo text-texto">{perfil?.nome ?? '—'}</p>
            {perfil && <Etiqueta>{ROTULO_DO_PAPEL[perfil.papel] ?? perfil.papel}</Etiqueta>}
          </div>
        </div>

        {carregando && <p className="text-apoio text-texto-suave">Carregando…</p>}
        {(erro || erroDaFoto) && <p className="text-apoio text-perigo">{erro ?? erroDaFoto}</p>}

        {perfil && (
          <div className="flex flex-col">
            <Linha rotulo="E-mail" valor={perfil.email} />
            <Linha rotulo="Na ROTEVIA desde" valor={data} />

            {perfil.loja && (
              <>
                <Linha rotulo="Loja" valor={perfil.loja.nome} />
                <Linha rotulo="Endereço" valor={perfil.loja.endereco ?? 'Não informado'} />
                <Linha rotulo="Taxa por entrega" valor={formatarDinheiro(perfil.loja.taxaPorEntrega)} />
              </>
            )}

            {perfil.entregador && (
              <>
                <Linha rotulo="CPF" valor={perfil.entregador.cpf} />
                <Linha rotulo="Telefone" valor={perfil.entregador.telefone} />
                <Linha rotulo="Moto" valor={perfil.entregador.modeloDaMoto} />
                <Linha rotulo="Placa" valor={perfil.entregador.placa} />
                <Linha
                  rotulo="Disponível"
                  valor={perfil.entregador.disponivelParaEntrega ? 'Sim' : 'Não'}
                />
              </>
            )}
          </div>
        )}
      </div>
    </Dialogo>
  )
}
