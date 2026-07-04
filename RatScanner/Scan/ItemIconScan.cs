using RatEye;
using RatStash;
using System;
using Icon = RatEye.Processing.Icon;
using TarkovItem = ShuShuscanner.TarkovDev.GraphQL.Item;

namespace ShuShuscanner.Scan;

public class ItemIconScan : ItemScan {
	public bool Rotated;
	public ItemExtraInfo ItemExtraInfo;
	public Icon Icon;

	private Vector2 _toolTipPosition;

	public ItemIconScan(Icon icon, Vector2 toolTipPosition, int duration, TarkovItem item) {
		Icon = icon;
		Item = item;
		ItemExtraInfo = icon.ItemExtraInfo;
		Confidence = icon.DetectionConfidence;
		Rotated = icon.Rotated;
		IconPath = icon.IconPath;

		_toolTipPosition = toolTipPosition;
		DissapearAt = DateTimeOffset.Now.ToUnixTimeMilliseconds() + duration;
	}

	public override Vector2 GetToolTipPosition() {
		return _toolTipPosition;
	}
}
