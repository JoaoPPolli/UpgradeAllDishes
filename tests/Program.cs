using System;
using System.Collections.Generic;
using UpgradeAllDishes;

static Dish Dish(int id, int level, params (int id, int count)[] items)
{
    var costs = new Dictionary<int,int>();
    foreach (var i in items) costs.Add(i.id, i.count);
    return new Dish(id, "Prato", level, costs);
}
int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FALHOU: " + name);
    Console.WriteLine("OK: " + name); passed++;
}
void Reject(Action action, string name)
{
    bool rejected = false;
    try { action(); } catch (InvalidOperationException) { rejected = true; } catch (OverflowException) { rejected = true; }
    Check(rejected, name);
}
var a = Dish(1, 2, (10, 4), (20, 3));
var b = Dish(2, 5, (10, 7), (30, 2));
var both = Plan.Sum(new[] {a, b});
Check(both.Count == 3 && both[10] == 11 && both[20] == 3 && both[30] == 2, "ingredientes compartilhados somados uma vez por prato");
Check(Plan.Sum(new[] {a})[10] == 4, "desmarcar prato remove seu custo");
Check(Plan.Sum(Array.Empty<Dish>()).Count == 0, "seleção vazia não consome ingredientes");
Check(Plan.Same(a, Dish(1, 2, (20, 3), (10, 4))), "ordem dos ingredientes não invalida orçamento");
Check(!Plan.Same(a, Dish(1, 3, (10, 4), (20, 3))), "mudança de nível invalida confirmação");
Check(!Plan.Same(a, Dish(1, 2, (10, 5), (20, 3))), "mudança de custo invalida confirmação");
Check(!Plan.Same(a, Dish(1, 2, (10, 4))), "mudança na lista de ingredientes invalida confirmação");
Reject(() => Plan.Sum(new[] {a, a}), "receita duplicada rejeitada");
Reject(() => Plan.Sum(new[] {Dish(3, 1, (10, -1))}), "custo negativo rejeitado");
Reject(() => Plan.Sum(new[] {Dish(3, 1, (10, 0))}), "custo zero rejeitado");
Reject(() => Plan.Sum(new[] {Dish(3, 1, (10, int.MaxValue)), b}), "soma excedendo limite rejeitada");
Check(!Plan.Fits(a, _ => 2), "prato sem estoque suficiente excluído");
Check(Plan.Fits(a, id => id == 10 ? 4 : 3), "estoque exato permite aprimoramento");
var affordable = Plan.Affordable(new[] {a, b}, id => id == 10 ? 8 : 10);
Check(affordable.Count == 1 && affordable[0].Id == a.Id, "seleção automática reserva ingredientes compartilhados");
Check(Plan.Affordable(new[] {a, b}, _ => 0).Count == 0, "estoque vazio não seleciona pratos");
Console.WriteLine($"{passed} testes aprovados.");
