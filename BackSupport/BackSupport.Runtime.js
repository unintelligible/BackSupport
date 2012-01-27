
(function () {
    this.BackSupport = this.BackSupport = this.BackSupport || {};
    this.BackSupport.Validate = this.BackSupport.Validate = this.BackSupport.Validate || {};
    this.BackSupport.Validate.Required = function (options) {
        this.fieldName = options.fieldName;
        return function (val) {
            if (val === null || val === undefined)
                return { valid: false, message: options.fieldName + ' is required' };
            return { valid: true };
        };
    };

    this.BackSupport.Validate.Range = function (options) {
        this.fieldName = options.fieldName;
        this.min = options.min;
        this.max = options.max;
        return function (val) {
            if (val === null || val === undefined)
                return { valid: null };
            var isBelowMin = false, isAboveMax = false;
            if (options.min && val < options.min)
                isBelowMin = true;
            if (options.max && val > options.max)
                isAboveMax = true;
            var res = { valid: !isAboveMax && !isBelowMin };
            if (isBelowMin)
                res.message = options.fieldName + ' should be above ' + options.min;
            else if (isAboveMax)
                res.message = options.fieldName + ' should be below ' + options.max;
            return res;
        };
    };

    this.BackSupport.Validate.StringLength = function (options) {
        this.fieldName = options.fieldName;
        return function (val) {
            if (val === null || val === undefined)
                return { valid: null };
            var v2 = val.toString();
            var isBelowMin = false, isAboveMax = false;
            if (options.minLength && v2.length < options.minLength)
                isBelowMin = true;
            if (options.maxLength && v2.length > options.maxLength)
                isAboveMax = true;
            var res = { valid: !isAboveMax && !isBelowMin };
            if (isBelowMin)
                res.message = options.fieldName + ' should be longer than ' + options.minLength + ' characters';
            else if (isAboveMax)
                res.message = options.fieldName + ' should be less than ' + options.maxLength + ' characters';
            return res;
        };
    };

    this.BackSupport.Validate.Regex = function (options) {
        this.fieldName = options.fieldName;
        return function (val) {
            if (val === null || val === undefined)
                return { valid: null };
            var re = new RegExp(options.regex);
            if (!re.test(val))
                return { valid: false, message: val + ' is not a valid ' + options.fieldName };
            return { valid: true };
        };
    };

    this.BackSupport.Validate.Type = this.BackSupport.Validate.Type = this.BackSupport.Validate.Type || {};
    this.BackSupport.Validate.Type.String = function (val) {
        if (typeof (val) == 'object')
            return false;
        return true;
    };

    this.BackSupport.Validate.Type.Char = function (val) {
        if (val == null || typeof (val) == 'object')
            return false;
        return val.toString().length == 2;
    };
    this.BackSupport.Validate.Type.NullableChar = function (val) {
        if (val == null)
            return true;
        return BackSupport.Validate.Type.Char(val);
    };
    this.BackSupport.Validate.Type.Integer = function (val) {
        if (val == null)
            return false;
        var s = val.toString();
        var t = parseInt(s);
        return !isNaN(t);
    };
    this.BackSupport.Validate.Type.NullableInteger = function (val) {
        if (val == null)
            return true;
        return BackSupport.Validate.Type.Integer(val);
    };
    this.BackSupport.Validate.Type.Float = function (val) {
        if (val == null)
            return false;
        var s = val.toString();
        var t = parseFloat(s);
        return !isNaN(t);
    };
    this.BackSupport.Validate.Type.NullableFloat = function (val) {
        if (val == null)
            return true;
        return BackSupport.Validate.Type.Float(val);
    };
    this.BackSupport.Validate.Type.Boolean = function (val) {
        if (val == null)
            return false;
        var s = val.toString();
        return !(/true|false|0|1/i).test(s);
    };
    this.BackSupport.Validate.Type.NullableBoolean = function (val) {
        if (val == null)
            return true;
        return BackSupport.Validate.Type.Boolean(val);
    };
    this.BackSupport.Validate.Type.DateTime = function (val) {
        if (val == null)
            return false;
        var s = val.toString();
        s = s.split('/');
        if (s.length != 3)
            return false;
        var d = new Date(s[2], s[1] - 1, s[0]);
        return ((d.getMonth() + 1 != s[1]) || (d.getDate() != s[0]) || (d.getFullYear() != s[2]));
    };
    this.BackSupport.Validate.Type.NullableDateTime = function (val) {
        if (val == null)
            return true;
        return BackSupport.Validate.Type.DateTime(val);
    };
    this.BackSupport.Validate.Type.Object = function (val) {
        if (val == null)
            return true;
        return typeof val == "object";
    };



    this.BackSupport.Parse = this.BackSupport.Parse = this.BackSupport.Parse || {};
    this.BackSupport.Parse.Type = this.BackSupport.Parse.Type = this.BackSupport.Parse.Type || {};
    this.BackSupport.Parse.Type.String = function (val) {
        if (typeof (val) == 'object')
            return null;
        return val.toString();
    };

    this.BackSupport.Parse.Type.Char = function (val) {
        if (val == null || typeof (val) == 'object')
            return null;
        return val.toString()[0];
    };
    this.BackSupport.Parse.Type.Integer = function (val) {
        if (val == null)
            return null;
        var s = val.toString();
        var t = parseInt(s);
        if (isNaN(t))
            return null;
        return t;
    };
    this.BackSupport.Parse.Type.Float = function (val) {
        if (val == null)
            return false;
        var s = val.toString();
        var t = parseFloat(s);
        if (isNaN(t))
            return null;
        return t;
    };
    this.BackSupport.Parse.Type.Boolean = function (val) {
        if (val == null)
            return false;
        var s = val.toString();
        if ((/true|1/i).test(s))
            return true;
        if ((/false|0/i).test(s))
            return false;
        return null;
    };
    this.BackSupport.Parse.Type.DateTime = function (val) {
        if (val == null)
            return false;
        var s = val.toString();
        s = s.split('/');
        if (s.length != 3)
            return false;
        var d = new Date(s[2], s[1] - 1, s[0]);
        if ((d.getMonth() + 1 != s[1]) || (d.getDate() != s[0]) || (d.getFullYear() != s[2]))
            return d;
        return null;
    };
    this.BackSupport.Parse.Type.Object = function (val) {
        if (val == null || typeof (val) != 'object')
            return null;
        return val;
    };
}).call(this);
