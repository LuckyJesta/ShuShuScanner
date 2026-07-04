using RatEye;
using RatEye.Processing;
using System;
using TarkovItem = ShuShuscanner.TarkovDev.GraphQL.Item;

namespace ShuShuscanner.Scan;

public class ItemNameScan : ItemScan {
	private Vector2 _toolTipPosition;

	public ItemNameScan(Inspection inspection, Vector2 toolTipPosition, int duration, TarkovItem item) {
		Item = item;
		Confidence = inspection.MarkerConfidence;
		IconPath = inspection.IconPath;
		_toolTipPosition = toolTipPosition;
		DissapearAt = DateTimeOffset.Now.ToUnixTimeMilliseconds() + duration;
	}

	public override Vector2 GetToolTipPosition() {
		return _toolTipPosition;
	}
}
