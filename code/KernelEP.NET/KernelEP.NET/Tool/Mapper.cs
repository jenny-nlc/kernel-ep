using System;
using System.Diagnostics;
using System.Linq;
using System.Collections.Generic;
using MicrosoftResearch.Infer;
using MicrosoftResearch.Infer.Collections;
using MicrosoftResearch.Infer.Models;
using MicrosoftResearch.Infer.Distributions;
using MicrosoftResearch.Infer.Maths;
using MicrosoftResearch.Infer.Factors;
using MicrosoftResearch.Infer.Utils;
using KernelEP.Op;

namespace KernelEP.Tool{
	//A finite-dimensional features generator for Euclidean (vector) inputs.
	public abstract class FeatureMap{
		public abstract Vector GenFeatures(Vector input);
		
		// return the number of features (numFeatures) to be generated.
		public abstract int NumFeatures();

		// return the input dimension expected.
		public abstract int InputDim();
	}


	// Corresponds to FeatureMap in Matlab code.
	// Consider VectorMapper<T1, ...>
	public abstract class VectorMapper{
		public abstract Vector MapToVector(params IKEPDist[] msgs);

		// return the dimension of the output mapped vector.
		public abstract int GetOutputDimension();

		// return the expected number of incoming messages
		// negative for any number.
		//		public abstract int NumInputMessages();

		// Load a FeatureMap in Matlab represented by the input
		// All FeatureMap objects can be serialized to struct with .toStruct()
		public static VectorMapper FromMatlabStruct(MatlabStruct s){
			string className = s.GetString("className");
			VectorMapper map = null;
			if(className.Equals("RandFourierGaussMVMap")){
				map = RFGMVMap.FromMatlabStruct(s);
			} else if(className.Equals("CondFMFiniteOut")){
				map = CondFMFiniteOut.FromMatlabStruct(s);
			} 
//			else if(className.Equals("CondCholFiniteOut")){
//				map = CondCholFiniteOut.FromMatlabStruct(s);
//			} 
			else if(className.Equals(RFGJointKGG.MATLAB_CLASS)){
				map = RFGJointKGG.FromMatlabStruct(s);
			} else if(className.Equals(StackVectorMapper.MATLAB_CLASS)){
				map = StackVectorMapper.FromMatlabStruct(s);
			} else if(className.Equals(BayesLinRegFM.MATLAB_CLASS)){
				map = BayesLinRegFM.FromMatlabStruct(s);
			} else if(className.Equals(UAwareVectorMapper.MATLAB_CLASS)){
				map = UAwareVectorMapper.FromMatlabStruct(s);
			} else if(className.Equals(UAwareStackVectorMapper.MATLAB_CLASS)){
				map = UAwareStackVectorMapper.FromMatlabStruct(s);
			} else{
				throw new ArgumentException("Unknown className: " + className);
			}
			//			else if(className.Equals("RFGSumEProdMap")){
			//
			//			}else if(className.Equals("RFGEProdMap")){
			//
			//			}else if(className.Equals("RFGJointEProdMap")){
			//
			//			}else if(className.Equals("RFGProductEProdMap")){
			//
			//			}
			return map;

		}
	}


	// Correspond to UAwareInstancesMapper in Matlab
	public abstract class UAwareVectorMapper : VectorMapper{
		public const string MATLAB_CLASS = "UAwareInstancesMapper";
		// Estimate uncertainty on the incoming messages d1 and d2.
		// Uncertainty might be a vector e.g., multioutput operator.
		public abstract double[] EstimateUncertainty(params IKEPDist[] dists);

		// Map and estiamte uncertainty.
		public abstract void MapAndEstimateU(out Vector mapped, 
		                                     out double[] uncertainty, params IKEPDist[] dists);

		public new static UAwareVectorMapper FromMatlabStruct(MatlabStruct s){
			string className = s.GetString("className");
			UAwareVectorMapper map = null;
			if(className.Equals(MATLAB_CLASS)){
				map = null;
			} else{
				throw new ArgumentException("Unknown className: " + className);
			}
		
			return map;

		}
	}


	// same class name in Matlab
	public class BayesLinRegFM : OnlineVectorMapper{
		public new const string MATLAB_CLASS = "BayesLinRegFM";

		protected VectorMapper featureMap;
		//		% matrix needed in mapInstances(). dz x numFeatures
		//		% where dz = dimension of output sufficient statistic.
		protected Vector posteriorMean;

		//		% posterior covariance matrix. Used for computing predictive variance.
		//		% DxD where D = number of features
		protected Matrix posteriorCov;

		//		% output noise variance (regularization parameter)
		protected double noiseVar;

		// uncertainty threshold for log(predictive variance)
		protected double uThreshold;

		// Cross correlation vector. XY'.
		protected Vector crossCorr;

		protected BayesLinRegFM(){
		}

		public override void MapAndEstimateU(out Vector mapped, out double[] uncertainty, 
		                                     params IKEPDist[] msgs){
			Vector feature = featureMap.MapToVector(msgs);
			mapped = posteriorMean * feature;
			double predVar = posteriorCov.QuadraticForm(feature) + noiseVar;
			uncertainty = new[]{ predVar };
		}

		public override Vector MapToVector(params IKEPDist[] msgs){
			Vector feature = featureMap.MapToVector(msgs);
			double predict = posteriorMean.Inner(feature);
			return Vector.FromArray(new []{ predict });
		}

		public override int GetOutputDimension(){
			return 1;
		}

		public override double[] EstimateUncertainty(params IKEPDist[] dists){
			// return log predictive variance
			Vector feature = featureMap.MapToVector(dists);
			double predVar = posteriorCov.QuadraticForm(feature) + noiseVar;
			return new []{ Math.Log(predVar) };
		}

		public override bool IsUncertain(params IKEPDist[] msgs){
			double logPredVar = EstimateUncertainty(msgs)[0];
			return logPredVar >= uThreshold;
		}

		public override void UpdateVectorMapper(Vector target, params IKEPDist[] msgs){
			if(target.Count() != 1){
				throw new ArgumentException("expect the target to be one-dimensional");
			}
			// update XY'
			Vector x = featureMap.MapToVector(msgs);
			double y = target[0];
			crossCorr = crossCorr + x * y;

			// update posterior covariance with the matrix inversion lemma
			double noise_prec = 1 / noiseVar;
			Vector covx = posteriorCov * x;
			double denom = (1.0 + x.Inner(covx) * noise_prec);
			posteriorCov = posteriorCov - covx.Outer(covx) * (noise_prec / denom);

			// posterior mean 
			posteriorMean = posteriorCov * (crossCorr * noise_prec);
		}

		public override double[] GetUncertaintyThreshold(){
			return new[]{ uThreshold };
		}

		public override void SetUncertaintyThreshold(params double[] thresh){
			if(thresh.Length != 1){
				throw new ArgumentException("Except one threshold");
			}
			this.uThreshold = thresh[0];
		}

		public override bool IsThresholdBased(){
			return true;
		}


		public static new BayesLinRegFM FromMatlabStruct(MatlabStruct s){
//			s.className=class(this);
//			s.featureMap=this.featureMap.toStruct();
//			%s.regParam=this.regParam;
//			s.mapMatrix=this.mapMatrix;
//			s.posteriorCov = this.posteriorCov; 
//			s.noise_var = this.noise_var;

			string className = s.GetString("className");
			if(!className.Equals(MATLAB_CLASS)){
				throw new ArgumentException("The input does not represent a " + MATLAB_CLASS);
			}
			MatlabStruct fmStruct = s.GetStruct("featureMap");
			VectorMapper featureMap = VectorMapper.FromMatlabStruct(fmStruct);
			// This is the same as a posterior mean
			Vector mapMatrix = s.Get1DVector("mapMatrix");
			if(mapMatrix.Count() != featureMap.GetOutputDimension()){
				throw new ArgumentException("mapMatrix and featureMap's dimenions are incompatible.");
			}
			Matrix postCov = s.GetMatrix("posteriorCov");
			if(postCov.Cols != featureMap.GetOutputDimension()){
				throw new ArgumentException("posterior covariance and featureMap's dimenions are incompatible.");
			}
			double noise_var = s.GetDouble("noise_var");
			Vector crossCorr = s.Get1DVector("crossCorrelation");
			var bayes = new BayesLinRegFM();
			bayes.featureMap = featureMap;
			bayes.posteriorMean = mapMatrix;
			bayes.posteriorCov = postCov;
			bayes.noiseVar = noise_var;
			bayes.crossCorr = crossCorr;
			return bayes;
		}
	}



	public class UAwareStackVectorMapper : UAwareVectorMapper{
		public const string MATLAB_CLASS = "UAwareStackInsMapper";
		private readonly UAwareVectorMapper[] mappers;

		public UAwareStackVectorMapper(params UAwareVectorMapper[] mappers){
			this.mappers = mappers;
		}

		public override Vector MapToVector(params IKEPDist[] msgs){
			Vector[] outs = mappers.Select(map => map.MapToVector(msgs)).ToArray();
			Vector stack = MatrixUtils.ConcatAll(outs);
			return stack;
		}

		public override int GetOutputDimension(){
			return mappers.Sum(map => map.GetOutputDimension());
		}


		public override double[] EstimateUncertainty(params IKEPDist[] dists){
			// ** Take only the fist uncertainty estimate from each mapper.
			double[] U = mappers.Select(map => map.EstimateUncertainty(dists)[0]).ToArray();
			return U;
		}

		public override void MapAndEstimateU(out Vector mapped, 
		                                     out double[] uncertainty, params IKEPDist[] dists){
			// ** Take only the fist uncertainty estimate from each mapper.
			int m = mappers.Length;
			uncertainty = new double[m];
			Vector[] outs = new Vector[m];
			for(int i = 0; i < m; i++){
				double[] ui;
				Vector outi;
				mappers[i].MapAndEstimateU(out outi, out ui, dists);
				outs[i] = outi;
				uncertainty[i] = ui[0];
			}
			mapped = MatrixUtils.ConcatAll(outs);
		}

		public new static UAwareStackVectorMapper FromMatlabStruct(MatlabStruct s){
			//			s = struct();
			//			s.className=class(this);
			//			mapperCount = length(this.instancesMappers);
			//			mapperCell = cell(1, mapperCount);
			//			for i=1:mapperCount
			//					mapperCell{i} = this.instancesMappers{i}.toStruct();
			//			end
			//			s.instancesMappers = this.instancesMappers;

			string className = s.GetString("className");
			if(!className.Equals(MATLAB_CLASS)){
				throw new ArgumentException("The input does not represent a " + MATLAB_CLASS);
			} 


			object[,] mappersCell = s.GetCells("instancesMappers");
			var mappers = new UAwareVectorMapper[mappersCell.GetLength(1)];
			for(int i = 0; i < mappers.Length; i++){
				var mapStruct = new MatlabStruct((Dictionary<string, object>)mappersCell[0, i]);
				VectorMapper m1 = VectorMapper.FromMatlabStruct(mapStruct);
				mappers[i] = (UAwareVectorMapper)m1;
			}
			return new UAwareStackVectorMapper(mappers);

		}
	}

	// StackInstancesMapper in Matlab code
	public class StackVectorMapper: VectorMapper{

		private readonly VectorMapper[] vectorMappers;
		public const string MATLAB_CLASS = "StackInstancesMapper";

		public StackVectorMapper(params VectorMapper[] vectorMappers){
			this.vectorMappers = vectorMappers;
		}


		public override int GetOutputDimension(){
			int outDim = vectorMappers.Sum(map => map.GetOutputDimension());
			return outDim;
		}


		public override Vector MapToVector(params IKEPDist[] msgs){
			var q = vectorMappers.Select(map => map.MapToVector(msgs));
			Vector[] mapped = q.ToArray();
			return MatrixUtils.ConcatAll(mapped);
		}

		public static new StackVectorMapper FromMatlabStruct(MatlabStruct s){
			//			s = struct();
			//			s.className=class(this);
			//			mapperCount = length(this.instancesMappers);
			//			mapperCell = cell(1, mapperCount);
			//			for i=1:mapperCount
			//					mapperCell{i} = this.instancesMappers{i}.toStruct();
			//			end
			//			s.instancesMappers = this.instancesMappers;

			string className = s.GetString("className");
			if(!className.Equals(MATLAB_CLASS)){
				throw new ArgumentException("The input does not represent a " + MATLAB_CLASS);
			}

			object[,] mappersCell = s.GetCells("instancesMappers");
			if(mappersCell.Length != 2){
				throw new ArgumentException("instancesMappers should have length 2.");
			}
			int m = mappersCell.GetLength(1);
			var maps = new VectorMapper[m];
			for(int i = 0; i < m; i++){
				var mapStruct = new MatlabStruct((Dictionary<string, object>)mappersCell[0, i]);
				VectorMapper map = VectorMapper.FromMatlabStruct(mapStruct);
				maps[i] = map;
			}

			return new StackVectorMapper(maps);

		}
	}



	// Corresponds to a class of the same name in Matlab code.
	// An InstancesMapper in Matlab code.
	public class CondFMFiniteOut : VectorMapper{
		private VectorMapper featureMap;
		// dz x numFeatures where dz is the dimension of output sufficient
		// statistic
		private Matrix mapMatrix;

		public const string MATLAB_CLASS = "CondFMFiniteOut";

		public CondFMFiniteOut(VectorMapper featureMap, Matrix mapMatrix){
//			Console.WriteLine("featureMap's output dim: {0}", featureMap.GetOutputDimension());
//			Console.WriteLine("mapMatrix's #cols: {0}", mapMatrix.Cols);
			if(featureMap.GetOutputDimension() != mapMatrix.Cols){
				throw new ArgumentException("featureMap output dimension does not match with mapMatrix.");
			}
			this.featureMap = featureMap;
			this.mapMatrix = mapMatrix;
		}

		public override int GetOutputDimension(){
			return mapMatrix.Rows;
		}


		public override Vector MapToVector(params IKEPDist[] msgs){
			Vector features = featureMap.MapToVector(msgs);
			return mapMatrix * features;
		}

		public static new CondFMFiniteOut FromMatlabStruct(MatlabStruct s){
//			s.className=class(this);
//            s.featureMap=this.featureMap.toStruct();
//            s.regParam=this.regParam;
//            s.mapMatrix=this.mapMatrix;

			string className = s.GetString("className");
			if(!className.Equals(MATLAB_CLASS)){
				throw new ArgumentException("The input does not represent a " + typeof(CondFMFiniteOut));
			}

			MatlabStruct mapStruct = s.GetStruct("featureMap");
			VectorMapper featureMap = VectorMapper.FromMatlabStruct(mapStruct);

			// dz x numFeatures where dz is the dimension of output sufficient 
			// statistic
			Matrix mapMatrix = s.GetMatrix("mapMatrix");
			Console.WriteLine("{0}.mapMatrix size: ({1}, {2})", MATLAB_CLASS, 
				mapMatrix.Rows, mapMatrix.Cols);
			return new CondFMFiniteOut(featureMap,mapMatrix);
		}
	}


	public abstract class DistMapper<T>
		where T:IKEPDist{
		// Map incoming messages to an output message
		public abstract T MapToDist(params IKEPDist[] msgs);

		public static DistMapper<T> FromMatlabStruct(MatlabStruct s){
			string className = s.GetString("className");
			if(className.Equals(GenericMapper<T>.MATLAB_CLASS)){
				return GenericMapper<T>.FromMatlabStruct(s);
			} else{
				throw new ArgumentException("Unknown DistMapper to load.");
			}
		}
	}

	public class GenericMapper<T> : DistMapper<T> 
		where T:IKEPDist{
		// suffMapper maps from messages into sufficient statistic output vector.
		protected VectorMapper suffMapper;
		protected DistBuilder<T> distBuilder;
		public const string MATLAB_CLASS = "GenericMapper";

		public GenericMapper(VectorMapper suffMapper, DistBuilder<T> distBuilder){
			this.suffMapper = suffMapper;
			this.distBuilder = distBuilder;
		}

		public override T MapToDist(params IKEPDist[] msgs){
			Vector mapped = suffMapper.MapToVector(msgs);
			// mapped vector and distBuilder must be compatible.
			T outDist = distBuilder.FromStat(mapped);
			return outDist;
		}

		public new static GenericMapper<T> FromMatlabStruct(MatlabStruct s){
			string className = s.GetString("className");
			if(!(className.Equals(MATLAB_CLASS)
			   || className.Equals(UAwareGenericMapper<T>.MATLAB_CLASS))){
				throw new ArgumentException("The input does not represent a " +
				typeof(GenericMapper<T>));
			}
			// nv = number of variables.
//			int inVars = s.GetInt("nv");
			MatlabStruct rawOperator = s.GetStruct("operator");
			VectorMapper instancesMapper = VectorMapper.FromMatlabStruct(rawOperator);
			MatlabStruct rawBuilder = s.GetStruct("distBuilder");
			DistBuilder<T> distBuilder = DistBuilderBase.FromMatlabStruct(rawBuilder)
				as DistBuilder<T>;
			return new GenericMapper<T>(instancesMapper,distBuilder);
		}
	}


	// version with variable number of arguments
	public abstract class UAwareDistMapper<T> :  GenericMapper<T>
		where T : IKEPDist{

		protected UAwareDistMapper(UAwareVectorMapper suffMapper, 
		                           DistBuilder<T> distBuilder) : base(suffMapper, distBuilder){
		}

		// Estimate uncertainty on the incoming messages d1 and d2.
		public abstract double[] EstimateUncertainty(params IKEPDist[] msgs);
	}

	public class UAwareGenericMapper<T>: UAwareDistMapper<T>
		where T: IKEPDist{

		public new const string MATLAB_CLASS = "UAwareGenericMapper";

		// suffMapper must implement IUAwareVectorMapper
		public UAwareGenericMapper(UAwareVectorMapper  suffMapper, 
		                           DistBuilder<T> distBuilder)
			: base(suffMapper, distBuilder){

		}

		public override double[] EstimateUncertainty(params IKEPDist[] msgs){
			//			double[] u = vm.EstimateUncertainty(new IKEPDist[]{d1, d2});
			//			return u;
			UAwareVectorMapper uvm = (UAwareVectorMapper)suffMapper;
			double[] u = uvm.EstimateUncertainty(msgs);
			return u;
		}

	}


	// Corresponds to a class of the same name in Matlab code.
	// Map two input distributions to a finite dimensional vector.
	//	public class CondCholFiniteOut<T1, T2> : VectorMapper<T1, T2>
	//		where T1 : IKEPDist
	//		where T2 : IKEPDist{
	//		// (Z-Out*R'(RR' + lambda*eye(ra))^-1 R)/lamb. Needed in MapToVector()
	//		private readonly Matrix zOutR3;
	//
	//		// input message pairs
	//		private readonly TensorInstances<T1, T2> inputTensor;
	//
	//		// kernel function that can operator on the input tensor
	//		public Kernel2<T1, T2> Kernel { get; private set; }
	//
	//		public const string MATLAB_CLASS = "CondCholFiniteOut";
	//
	//		public CondCholFiniteOut(Matrix zOutR3,
	//		                         TensorInstances<T1, T2> inputTensor, Kernel2<T1, T2> kernel){
	//			this.zOutR3 = zOutR3;
	//			this.inputTensor = inputTensor;
	//			this.Kernel = kernel;
	//		}
	//
	//		public override int GetOutputDimension(){
	//			return zOutR3.Rows;
	//		}
	//
	//
	//		public override Vector MapToVector(T1 msg1, T2 msg2){
	//
	//			var incoming = new Tuple<T1, T2>(msg1,msg2);
	//			Vector k = Kernel.Eval(inputTensor.GetAll(), incoming);
	//			return zOutR3 * k;
	//		}
	//
	//		public static new CondCholFiniteOut<T1, T2> FromMatlabStruct(MatlabStruct s){
	////			s = struct();
	////			s.className=class(this);
	////			s.instances = this.In.toStruct();
	////			s.kfunc = this.kfunc.toStruct();
	////			% a matrix
	////			s.ZOutR3 = this.ZOutR3;
	//
	//			string className = s.GetString("className");
	//			if(!className.Equals(MATLAB_CLASS)){
	//				throw new ArgumentException("The input does not represent a " +
	//				typeof(CondCholFiniteOut<T1, T2>));
	//			}
	//			// assume a TensorInstances
	////			var instancesDict = (Dictionary<string, object>)s.GetStruct("instances");
	//			TensorInstances<T1, T2> instances = 
	//				TensorInstances<T1, T2>.FromMatlabStruct(s.GetStruct("instances"));
	//			Kernel2<T1, T2> kfunc = Kernel2<T1, T2>.FromMatlabStruct(
	//				                        s.GetStruct("kfunc"));
	//			Matrix zOutR3 = s.GetMatrix("ZOutR3");
	//			return new CondCholFiniteOut<T1, T2>(zOutR3,instances,kfunc);
	//
	//		}
	//	}

}
// end name space

