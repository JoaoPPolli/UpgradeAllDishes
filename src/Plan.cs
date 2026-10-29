using System;
using System.Collections.Generic;
using System.Linq;

namespace UpgradeAllDishes;

public sealed class Dish
{
    public int Id { get; }
    public string Name { get; }
    public int Level { get; }
    public IReadOnlyDictionary<int, int> Costs { get; }
    public Dish(int id, string name, int level, IReadOnlyDictionary<int, int> costs)
    { Id = id; Name = name; Level = level; Costs = costs; }
}

public static class Plan
{
    public static bool Fits(Dish dish, Func<int, int> stock) => dish.Costs.All(c => stock(c.Key) >= c.Value);

    public static List<Dish> Affordable(IEnumerable<Dish> dishes, Func<int, int> stock)
    {
        var remaining = new Dictionary<int, int>();
        var result = new List<Dish>();
        foreach (var dish in dishes)
        {
            foreach (var cost in dish.Costs)
                if (!remaining.ContainsKey(cost.Key)) remaining[cost.Key] = stock(cost.Key);
            if (!Fits(dish, id => remaining[id])) continue;
            result.Add(dish);
            foreach (var cost in dish.Costs) remaining[cost.Key] -= cost.Value;
        }
        return result;
    }

    public static Dictionary<int, int> Sum(IEnumerable<Dish> dishes)
    {
        var total = new Dictionary<int, int>();
        var ids = new HashSet<int>();
        foreach (var dish in dishes)
        {
            if (!ids.Add(dish.Id)) throw new InvalidOperationException("Receita duplicada.");
            foreach (var pair in dish.Costs)
            {
                if (pair.Value <= 0) throw new InvalidOperationException("Custo inválido.");
                total.TryGetValue(pair.Key, out int old);
                total[pair.Key] = checked(old + pair.Value);
            }
        }
        return total;
    }

    public static bool Same(Dish left, Dish right) => left.Id == right.Id && left.Level == right.Level
        && left.Costs.Count == right.Costs.Count
        && left.Costs.All(p => right.Costs.TryGetValue(p.Key, out int n) && n == p.Value);
}
