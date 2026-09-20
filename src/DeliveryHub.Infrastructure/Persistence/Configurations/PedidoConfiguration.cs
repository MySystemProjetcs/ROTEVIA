using DeliveryHub.Domain.Orders;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace DeliveryHub.Infrastructure.Persistence.Configurations;

internal sealed class PedidoConfiguration : IEntityTypeConfiguration<Pedido>
{
    public void Configure(EntityTypeBuilder<Pedido> builder)
    {
        builder.ToTable("pedidos");
        builder.HasKey(x => x.Id);

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.MerchantId).HasColumnName("merchant_id").IsRequired();
        builder.Property(x => x.IdExterno).HasColumnName("id_externo").HasMaxLength(128).IsRequired();
        builder.Property(x => x.NumeroExibicao).HasColumnName("numero_exibicao").HasMaxLength(32).IsRequired();
        builder.Property(x => x.EhTeste).HasColumnName("eh_teste").IsRequired();
        builder.Property(x => x.ValorTotal).HasColumnName("valor_total").IsRequired();
        builder.Property(x => x.TaxaEntrega).HasColumnName("taxa_entrega").IsRequired();
        builder.Property(x => x.CriadoNaOrigemEm).HasColumnName("criado_na_origem_em").IsRequired();
        builder.Property(x => x.RecebidoEm).HasColumnName("recebido_em").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasConversion<string>().HasMaxLength(20).IsRequired();

        // Aponta pro Courier.Id — sem FK de propósito, é referência entre
        // agregados (mesmo padrão de outras referências cross-aggregate aqui).
        builder.Property(x => x.EntregadorId).HasColumnName("entregador_id");

        // Snapshot do ganho, gravado na conclusão — nunca filtrado nem somado
        // junto com ValorTotal (que é receita da loja, não do motoboy).
        builder.Property(x => x.ValorPagoAoEntregador).HasColumnName("valor_pago_ao_entregador");

        // Código de confirmação da entrega: se o pedido exige, e quando foi
        // confirmado. O código em si nunca é guardado — quem valida é o iFood,
        // e reter dado do cliente sem uso seria só risco (CLAUDE.md §10).
        builder.Property(x => x.ExigeCodigoDeEntrega).HasColumnName("exige_codigo_entrega").IsRequired();
        builder.Property(x => x.EntregaPeloParceiro).HasColumnName("entrega_pelo_parceiro").IsRequired();
        builder.Property(x => x.CodigoConfirmadoEm).HasColumnName("codigo_confirmado_em");

        // Pagamento por table splitting, igual a Cliente e Endereço: é dado do
        // pedido, não entidade com vida própria.
        builder.OwnsOne(x => x.Pagamento, pagamento =>
        {
            pagamento.Property(x => x.ValorJaPago).HasColumnName("pagamento_valor_pago");
            pagamento.Property(x => x.ValorACobrar).HasColumnName("pagamento_valor_a_cobrar");
            pagamento.Property(x => x.Descricao).HasColumnName("pagamento_descricao").HasMaxLength(200);
        });

        builder.OwnsOne(x => x.Cliente, cliente =>
        {
            cliente.Property(x => x.Nome).HasColumnName("cliente_nome").HasMaxLength(200).IsRequired();
            cliente.Property(x => x.Telefone).HasColumnName("cliente_telefone").HasMaxLength(40);
            cliente.Property(x => x.Localizador).HasColumnName("cliente_localizador").HasMaxLength(40);
        });

        builder.OwnsOne(x => x.EnderecoEntrega, endereco =>
        {
            endereco.Property(x => x.Logradouro).HasColumnName("entrega_logradouro").HasMaxLength(200);
            endereco.Property(x => x.Numero).HasColumnName("entrega_numero").HasMaxLength(20);
            endereco.Property(x => x.Bairro).HasColumnName("entrega_bairro").HasMaxLength(120);
            endereco.Property(x => x.Cidade).HasColumnName("entrega_cidade").HasMaxLength(120);
            endereco.Property(x => x.Estado).HasColumnName("entrega_estado").HasMaxLength(10);
            endereco.Property(x => x.Cep).HasColumnName("entrega_cep").HasMaxLength(20);
            endereco.Property(x => x.Complemento).HasColumnName("entrega_complemento").HasMaxLength(200);
            endereco.Property(x => x.Referencia).HasColumnName("entrega_referencia").HasMaxLength(200);
            endereco.Property(x => x.Latitude).HasColumnName("entrega_latitude");
            endereco.Property(x => x.Longitude).HasColumnName("entrega_longitude");
        });

        builder.HasMany(x => x.Itens)
            .WithOne()
            .HasForeignKey(x => x.PedidoId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.Navigation(x => x.Itens).UsePropertyAccessMode(PropertyAccessMode.Field);

        // Evento duplicado não pode criar pedido duplicado, mesmo que passe pelo
        // inbox: a garantia final é aqui.
        builder.HasIndex(x => x.IdExterno)
            .IsUnique()
            .HasDatabaseName("ux_pedidos_id_externo");

        // Dois índices sobre as mesmas colunas, e é por isso que cada um
        // precisa ser declarado com nome no próprio HasIndex: chamar
        // HasIndex(props) duas vezes devolve o MESMO builder, e o segundo
        // sobrescreveria o primeiro — o modelo ficaria com um índice só,
        // e a migração seguinte apagaria o outro do banco.

        // Painel de pedidos ativos: o índice cresce com os pedidos abertos, não
        // com o histórico (ENGINEERING-GUIDE §7).
        builder
            .HasIndex(x => new { x.MerchantId, x.RecebidoEm }, "ix_pedidos_ativos_por_merchant")
            .HasFilter("status IN ('Recebido','Confirmado','EmPreparo','Pronto','Despachado','Aceito','EmRota','Chegou','Cobrar')");

        // Dashboard/resumo do dia e extrato de ganhos: mesma chave, sem filtro
        // de status (o parcial acima não serve pra Concluído/Cancelado).
        builder.HasIndex(x => new { x.MerchantId, x.RecebidoEm }, "ix_pedidos_merchant_recebido");
    }
}
