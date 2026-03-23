(function () {
    if (!window.jQuery || !jQuery.validator) return;

    function normalizeToInvariantDecimal(value) {
        if (typeof value !== "string") return value;
        return value.replace(/\./g, "").replace(",", ".");
    }

    var originalNumber = jQuery.validator.methods.number;
    jQuery.validator.methods.number = function (value, element) {
        if (this.optional(element)) return true;
        // Try original first (for dot decimal), then normalized value
        return (
            originalNumber.call(this, value, element) ||
            originalNumber.call(
                this,
                normalizeToInvariantDecimal(value),
                element
            )
        );
    };

    // Override range to validate against normalized value
    var originalRange = jQuery.validator.methods.range;
    jQuery.validator.methods.range = function (value, element, param) {
        if (this.optional(element)) return true;
        return originalRange.call(
            this,
            normalizeToInvariantDecimal(value),
            element,
            param
        );
    };
})();
